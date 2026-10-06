using E_Greeting.Models;
using E_Greeting.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace E_Greeting.Controllers;

public class SubscriptionModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Duration { get; set; } = "tháng";
    public List<string> Features { get; set; } = new();
}

public class SubscriptionController : Controller
{
    private readonly IServiceService _serviceService;
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private const int ExpiryMinutes = 2;

    public SubscriptionController(IServiceService serviceService, AppDbContext context, IConfiguration config, IMemoryCache cache)
    {
        _serviceService = serviceService;
        _context = context;
        _config = config;
        _cache = cache;
    }
    private DateTime GetOrCreateCreatedAt(int paymentId)
    {
        return _cache.GetOrCreate($"payment_created_{paymentId}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ExpiryMinutes + 1);
            return DateTime.Now;
        });
    }

    public IActionResult Index()
    {
        var plans = _serviceService.GetAllPlans();

        int currentRank = -1;          // -1 = chưa đăng nhập
        DateOnly? endDate = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            currentRank = 0;           // đã đăng nhập: mặc định Basic

            int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var today = DateOnly.FromDateTime(DateTime.Now);

            var top = _context.Subscriptions
                .Where(s => s.AccountId == accountId
                         && s.IsActive
                         && s.EndDate >= today
                         && s.ServiceId != null)
                .AsEnumerable()
                .OrderByDescending(s => s.ServiceId)
                .ThenByDescending(s => s.EndDate)
                .FirstOrDefault();

            if (top != null)
            {
                currentRank = top.ServiceId!.Value;
                endDate = top.EndDate;
            }
        }

        ViewBag.CurrentPlanId = currentRank;
        ViewBag.CurrentEndDate = endDate;
        return View("~/Views/Subscription/Index.cshtml", plans);
    }
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public IActionResult Subscribe(int planId)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account",
                new { returnUrl = Url.Action("Index", "Subscription") });
        }
        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Mặc định là Basic (0), lấy gói trả phí cao nhất đang hoạt động
        int currentRank = _context.Subscriptions
            .Where(s => s.AccountId == accountId
                     && s.IsActive
                     && s.EndDate >= today
                     && s.ServiceId != null)
            .Select(s => s.ServiceId!.Value)
            .AsEnumerable()
            .DefaultIfEmpty(0)
            .Max();

        if (planId <= currentRank)
        {
            TempData["ErrorMessage"] = planId == currentRank
                ? "Bạn đang sử dụng gói này rồi!"
                : "Bạn đã ở gói cao hơn, không thể đăng ký gói thấp hơn!";
            return RedirectToAction("Index");
        }

        var service = _context.Services.Find(planId);
        if (service == null)
        {
            TempData["ErrorMessage"] = "Gói dịch vụ không tồn tại.";
            return RedirectToAction("Index");
        }

        var subscription = new Subscription
        {
            AccountId = accountId,
            ServiceId = service.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Now),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(1)),
            IsActive = false
        };
        _context.Subscriptions.Add(subscription);
        _context.SaveChanges();

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            Amount = service.Price,
            PaymentDate = DateOnly.FromDateTime(DateTime.Now),
            Status = "UNPAID",
            PaymentMethod = "SePay"
        };
        _context.Payments.Add(payment);
        _context.SaveChanges();
        _cache.Set($"payment_created_{payment.Id}", DateTime.Now, TimeSpan.FromMinutes(ExpiryMinutes + 1));

        return RedirectToAction("Checkout", new { paymentId = payment.Id });
    }

    [Authorize]
    public IActionResult Checkout(int paymentId)
    {
        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var payment = _context.Payments
            .Include(p => p.Subscription)
            .ThenInclude(s => s.Service)
            .FirstOrDefault(p => p.Id == paymentId);

        if (payment == null) return NotFound();
        if (payment.Subscription.AccountId != accountId) return Forbid();

        if (payment.Status == "PAID")
            return RedirectToAction("Success", new { paymentId = payment.Id });

        if (payment.Status == "UNPAID")
        {
            var createdAt = GetOrCreateCreatedAt(payment.Id);
            if (DateTime.Now > createdAt.AddMinutes(ExpiryMinutes))
            {
                payment.Status = "EXPIRED";
                _context.SaveChanges();
            }
        }

        if (payment.Status == "EXPIRED")
        {
            ViewBag.Expired = true;
            return View(payment);
        }

        var code = $"DH{payment.Id}";
        var bankAccount = _config["SePay:BankAccount"];
        var bankShortName = _config["SePay:BankShortName"];

        ViewBag.QrUrl = $"https://qr.sepay.vn/img?acc={bankAccount}&bank={bankShortName}&amount={(long)payment.Amount}&des={code}";
        ViewBag.BankAccount = bankAccount;
        ViewBag.BankShortName = bankShortName;
        ViewBag.Code = code;
        ViewBag.Expired = false;
        ViewBag.ExpiresAtUnixMs = new DateTimeOffset(GetOrCreateCreatedAt(payment.Id).AddMinutes(ExpiryMinutes)).ToUnixTimeMilliseconds();

        return View(payment);
    }

    [Authorize]
    [HttpGet]
    public IActionResult CheckStatus(int paymentId)
    {
        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var payment = _context.Payments
            .Include(p => p.Subscription)
            .FirstOrDefault(p => p.Id == paymentId);

        if (payment == null || payment.Subscription.AccountId != accountId)
            return Json(new { status = "not_found" });

        if (payment.Status == "UNPAID")
        {
            var createdAt = GetOrCreateCreatedAt(payment.Id);
            if (DateTime.Now > createdAt.AddMinutes(ExpiryMinutes))
            {
                payment.Status = "EXPIRED";
                _context.SaveChanges();
            }
        }

        return Json(new { status = payment.Status });
    }

    [Authorize]
    public IActionResult Success(int paymentId)
    {
        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var payment = _context.Payments
            .Include(p => p.Subscription)
            .ThenInclude(s => s.Service)
            .FirstOrDefault(p => p.Id == paymentId);

        return payment == null || payment.Subscription.AccountId != accountId || payment.Status != "PAID"
            ? RedirectToAction("Index")
            : View(payment);
    }
}