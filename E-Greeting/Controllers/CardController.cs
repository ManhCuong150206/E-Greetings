using E_Greeting.Models;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace E_Greeting.Controllers;

// --- DTO Requests ---
public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? Location { get; set; }
    public string? EventTime { get; set; }
    public string? ImagePath { get; set; }
    public string? CardImageBase64 { get; set; }
    public string? AudioPath { get; set; }
    public string? AudioUrl { get; set; }
    public string? SubscriptionId { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty;
    public string? Message { get; set; }

    public DateTime? ScheduledAt { get; set; }
    public string? ScheduledAtStr { get; set; }
}

public class ReplyRequest
{
    public int CardId { get; set; }
    public string ReplyMessage { get; set; } = string.Empty;
    public string? AttendanceStatus { get; set; }
}

// --- Controller ---
[Authorize]
public class CardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public CardController(
        AppDbContext context,
        IEmailService emailService,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _env = env;
        _configuration = configuration;
    }

    // GET: /Card/Personalize/1
    [HttpGet]
    public async Task<IActionResult> Personalize(int id)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(t => t.Id == id);
        if (template == null) return NotFound("Không tìm thấy mẫu thiệp này!");

        bool isVip = false;
        int? userId = GetCurrentUserId();
        if (userId.HasValue)
        {
            isVip = await _context.Subscriptions.AnyAsync(s => s.AccountId == userId.Value && s.IsActive);
        }

        ViewBag.IsVip = isVip;
        return View(template);
    }

    // POST: /Card/SaveCardConfig (Hàm lưu/tạo thiệp từ trang Personalize)
    [HttpPost]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> SaveCardConfig([FromBody] SaveCardRequest req)
    {
        if (req == null)
            return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ!" });

        // 1. Lấy AccountId người dùng đang đăng nhập
        int? accountId = GetCurrentUserId();
        Account? account = null;
        if (accountId.HasValue)
        {
            account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId.Value);
        }

        // 2. Xử lý lưu ảnh Base64
        string? finalImagePath = SaveBase64Image(req.CardImageBase64)
                               ?? SaveBase64Image(req.ImagePath)
                               ?? req.ImagePath;

        // 3. Xử lý Parse Ngày/Giờ hẹn gửi linh hoạt
        DateTime? scheduledTime = null;

        if (!string.IsNullOrWhiteSpace(req.ScheduledAtStr))
        {
            string[] supportedFormats = new[] {
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "dd/MM/yyyy hh:mm tt",
                "dd/MM/yyyy h:mm tt"
            };

            var viCulture = new System.Globalization.CultureInfo("vi-VN");

            if (DateTime.TryParseExact(req.ScheduledAtStr, supportedFormats,
                viCulture, System.Globalization.DateTimeStyles.None, out var parsedExact))
            {
                scheduledTime = parsedExact;
            }
            else if (DateTime.TryParse(req.ScheduledAtStr, viCulture, System.Globalization.DateTimeStyles.None, out var parsedFallback))
            {
                scheduledTime = parsedFallback;
            }
        }

        if (!scheduledTime.HasValue && req.ScheduledAt.HasValue)
        {
            scheduledTime = req.ScheduledAt.Value;
        }

        // Chuẩn hóa múi giờ về Local
        if (scheduledTime.HasValue)
        {
            if (scheduledTime.Value.Kind == DateTimeKind.Utc)
            {
                scheduledTime = scheduledTime.Value.ToLocalTime();
            }

            scheduledTime = DateTime.SpecifyKind(scheduledTime.Value, DateTimeKind.Unspecified);
        }

        // 4. Khởi tạo Entity SentCard
        var newCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = req.TemplateId,
            RecipientEmail = req.RecipientEmail,
            Subject = req.Subject,
            AudioPath = req.AudioPath ?? req.AudioUrl,
            ImagePath = finalImagePath,
            CardHtmlContent = req.CardHtmlContent,
            Message = !string.IsNullOrWhiteSpace(req.Message)
                ? req.Message
                : JsonSerializer.Serialize(new
                {
                    recipientName = req.RecipientName,
                    eventAddress = req.Location,
                    eventTime = req.EventTime
                })
        };

        // 5. PHÂN LẠI ĐIỀU KIỆN: HẸN GIỜ VS GỬI NGAY
        bool isScheduledForFuture = false;
        DateTime now = DateTime.Now;

        if (scheduledTime.HasValue)
        {
            // Kiểm tra giờ hẹn phải ở tương lai
            if (scheduledTime.Value <= now)
            {
                return Json(new
                {
                    success = false,
                    message = $"Thời gian hẹn ({scheduledTime.Value:HH:mm dd/MM/yyyy}) phải lớn hơn thời điểm hiện tại ({now:HH:mm dd/MM/yyyy})!"
                });
            }

            // HẸN GIỜ TƯƠNG LAI: Giữ SentAt = NULL, chỉ gán ScheduledAt
            newCard.ScheduledAt = scheduledTime.Value;
            newCard.SentAt = null;
            isScheduledForFuture = true;
        }
        else
        {
            // GỬI NGAY: ScheduledAt = NULL, SentAt = Giờ hiện tại
            newCard.ScheduledAt = null;
            newCard.SentAt = now;
        }

        // 6. Lưu vào Database
        _context.SentCards.Add(newCard);
        await _context.SaveChangesAsync();

        // 7. GỬI EMAIL NGAY (Chỉ khi không hẹn giờ)
        if (newCard.SentAt.HasValue && !isScheduledForFuture)
        {
            string domain = _configuration["AppSettings:Domain"] ?? $"{Request.Scheme}://{Request.Host}";
            string cardViewUrl = $"{domain}/Card/ViewCard/{newCard.Id}";
            string senderName = account?.Name ?? User.Identity?.Name ?? "Hệ thống E-Greeting";

            try
            {
                await _emailService.SendCardEmailAsync(newCard.RecipientEmail, newCard.Subject, cardViewUrl, senderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Error]: {ex.Message}");
            }
        }

        // 8. Trả về kết quả JSON
        return Json(new
        {
            success = true,
            message = isScheduledForFuture
                ? $"Đã lên lịch hẹn giờ gửi thiệp vào lúc {newCard.ScheduledAt.Value:HH:mm dd/MM/yyyy}!"
                : "Đã gửi thiệp thành công!",
            cardId = newCard.Id,
            redirectUrl = "/History"
        });
    }

    // GET: /Card/ViewCard/1024
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ViewCard(int id)
    {
        var card = await _context.SentCards
            .Include(c => c.Account)
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card == null)
        {
            ViewBag.CardId = id;
            ViewBag.SenderEmail = "nguoidung@example.com";
            ViewBag.ImagePath = "";
            ViewBag.AudioPath = "";
            ViewBag.CardHtmlContent = "<div class='p-4 text-center text-muted'>Thiệp không tồn tại hoặc đã bị xóa.</div>";
            ViewBag.OpenAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.ConfigData = "{}";

            return NotFound("Thiệp không tồn tại hoặc đã bị xóa.");
        }

        if (card.OpenAt == null)
        {
            card.OpenAt = DateTime.Now;
            _context.SentCards.Update(card);
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(card.ImagePath) &&
           (card.ImagePath.TrimStart().StartsWith("{") || card.ImagePath.Contains("recipientName")))
        {
            card.ImagePath = card.Template?.ImagePath;
        }

        if (string.IsNullOrEmpty(card.ImagePath) && card.Template != null)
        {
            card.ImagePath = card.Template.ImagePath;
        }

        string bgImage = !string.IsNullOrEmpty(card.ImagePath)
            ? card.ImagePath
            : (card.Template?.ImagePath ?? "");

        string configData = card.Template?.ConfigData ?? "{}";

        ViewBag.CardId = card.Id;
        ViewBag.SenderEmail = card.SenderEmail ?? card.Account?.Email;
        ViewBag.RecipientEmail = card.RecipientEmail;
        ViewBag.Subject = card.Subject;
        ViewBag.ImagePath = bgImage;
        ViewBag.AudioPath = card.AudioPath ?? "";
        ViewBag.CardHtmlContent = card.CardHtmlContent;
        ViewBag.OpenAt = card.OpenAt?.ToString("dd/MM/yyyy HH:mm");
        ViewBag.RecipientReply = card.RecipientReply;
        ViewBag.ConfigData = configData;

        return View(card);
    }

    // GET: /History HOẶC /Card/History
    [HttpGet]
    [Route("History")]
    public async Task<IActionResult> History(string tab = "cards")
    {
        int? accountId = GetCurrentUserId();

        var sentCards = await _context.SentCards
            .Include(c => c.Template)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.SentAt ?? c.ScheduledAt)
            .ToListAsync();

        var userSubscriptions = await _context.Subscriptions
             .Include(s => s.Service)
             .Include(s => s.Payments)
             .Where(s => s.AccountId == accountId)
             .OrderByDescending(s => s.Id)
             .ToListAsync();

        ViewBag.ActiveTab = tab;

        var viewModel = new UserHistoryViewModel
        {
            SentCards = sentCards,
            Subscriptions = userSubscriptions
        };

        return View("History", viewModel);
    }

    // POST: /Card/ReplyCard
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ReplyCard([FromBody] ReplyRequest request)
    {
        if (request == null || request.CardId <= 0)
        {
            return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ!" });
        }

        var card = await _context.SentCards.FirstOrDefaultAsync(c => c.Id == request.CardId);
        if (card == null)
        {
            return Json(new { success = false, message = "Không tìm thấy thông tin thiệp!" });
        }

        string rawStatus = request.AttendanceStatus ?? "";
        string formattedStatus = rawStatus switch
        {
            "Yes" => "Sẽ tham dự",
            "Maybe" => "Phân vân",
            "No" => "Không thể tham dự",
            _ => rawStatus
        };

        var replyData = new
        {
            Status = formattedStatus,
            Text = request.ReplyMessage ?? ""
        };

        card.RecipientReply = JsonSerializer.Serialize(replyData);
        _context.SentCards.Update(card);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Đã gửi phản hồi thành công!" });
    }

    // --- HÀM BỔ TRỢ HỆ THỐNG ---
    private int? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("UserId")
                     ?? User.FindFirstValue(ClaimTypes.Name);

        return int.TryParse(userIdStr, out int userId) ? userId : null;
    }

    private string CleanImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        path = path.Trim();
        if (path.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            path = path[4..];
            if (path.EndsWith(")")) path = path[..^1];
            path = path.Trim('\'', '"', ' ');
        }
        return path.Trim('\'', '"', ' ');
    }

    private string? SaveBase64Image(string? base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String)) return null;

        base64String = CleanImagePath(base64String);

        if (base64String.TrimStart().StartsWith("{") || base64String.Contains("recipientName"))
        {
            return null;
        }

        if (base64String.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parts = base64String.Split(',');
                if (parts.Length < 2) return null;

                string pureBase64 = parts[1].Trim().Replace(" ", "+");
                int mod4 = pureBase64.Length % 4;
                if (mod4 > 0)
                {
                    pureBase64 += new string('=', 4 - mod4);
                }

                byte[] imageBytes = Convert.FromBase64String(pureBase64);
                string extension = parts[0].Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

                string webRoot = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                string folderPath = Path.Combine(webRoot, "uploads", "img_sentcards");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"card_{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(folderPath, fileName);
                System.IO.File.WriteAllBytes(filePath, imageBytes);

                return $"/uploads/img_sentcards/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi lưu file Base64: " + ex.Message);
                return null;
            }
        }

        return base64String.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
               base64String.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? base64String
            : null;
    }
}