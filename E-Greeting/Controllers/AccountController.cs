using E_Greeting.Models;
using E_Greeting.Service;
using MailKit.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MimeKit;
using System.Security.Claims;
using System.Security.Cryptography;

namespace E_Greeting.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public AccountController(IAccountService accountService, AppDbContext context,
        IMemoryCache cache, IConfiguration config)
    {
        _accountService = accountService;
        _context = context;
        _cache = cache;
        _config = config;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (account, result) = await _accountService.LoginAsync(model.Email, model.Password);

        switch (result)
        {
            case LoginResult.EmailNotFound:
                ViewBag.Error = "Email does not exist in the system";
                return View(model);

            case LoginResult.WrongPassword:
                ViewBag.Error = "Incorrect password";
                return View(model);

            case LoginResult.AccountLocked:
                ViewBag.Error = "Your account has been locked";
                return View(model);
        }

        var userRole = account.Role.Trim().ToLower() == "admin" ? "Admin" : account.Role.Trim();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Name ?? account.Email),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, userRole)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return userRole.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Dashboard", "Admin")
            : !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var (account, result) = await _accountService.RegisterAsync(model.Name, model.Email, model.Password);

        if (result == RegisterResult.EmailExists)
        {
            ViewBag.Error = "This email is already registered";
            return View(model);
        }

        TempData["SuccessMessage"] = "Registration successful, please login";
        return RedirectToAction("Login");
    }

    // ==========================================
    // MỤC 4: QUẢN LÝ THÔNG TIN CÁ NHÂN & ĐỔI MẬT KHẨU
    // ==========================================

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login");

        var account = await _context.Accounts.FindAsync(userId);
        return account == null ? RedirectToAction("Login") : View(account);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> UpdateProfile(string name)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login");

        var account = await _context.Accounts.FindAsync(userId);
        if (account == null) return NotFound();

        account.Name = name;
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
        return RedirectToAction("Profile");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "Mật khẩu mới và Mật khẩu xác nhận không trùng khớp!";
            return RedirectToAction("Profile");
        }

        var account = await _context.Accounts.FindAsync(userId);
        if (account == null) return RedirectToAction("Login");

        if (account.Password != currentPassword)
        {
            TempData["ErrorMessage"] = "Mật khẩu hiện tại không đúng!";
            return RedirectToAction("Profile");
        }

        account.Password = newPassword;
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Profile");
    }

    // ==========================================
    // QUÊN MẬT KHẨU (OTP QUA GMAIL)
    // ==========================================

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        email = (email ?? "").Trim();

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account == null)
        {
            TempData["ErrorMessage"] = "Tài khoản không tồn tại.";
            return RedirectToAction("ForgotPassword");
        }

        // Tạo OTP 6 số, lưu 5 phút
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        _cache.Set(OtpKey(email), new OtpEntry { Code = otp }, TimeSpan.FromMinutes(5));

        try
        {
            await SendOtpMailAsync(email, otp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Gửi OTP lỗi: " + ex.Message + " | INNER: " + ex.InnerException?.Message);
            TempData["ErrorMessage"] = "Không gửi được email, vui lòng thử lại sau.";
            return RedirectToAction("ForgotPassword");
        }

        TempData["SuccessMessage"] = "Mã OTP đã được gửi tới email của bạn (hiệu lực 5 phút).";
        return RedirectToAction("ResetPassword", new { email });
    }

    [HttpGet]
    public IActionResult ResetPassword(string email)
    {
        ViewBag.Email = email;
        ViewBag.Verified = false;
        return View();
    }

    // Bước 1: kiểm tra OTP
    [HttpPost]
    public IActionResult VerifyOtp(string email, string otp)
    {
        if (!CheckOtp(email, otp, out var error))
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction("ResetPassword", new { email });
        }

        ViewBag.Email = email;
        ViewBag.Otp = otp;
        ViewBag.Verified = true;
        return View("ResetPassword");
    }

    // Bước 2: đổi mật khẩu (kiểm tra lại OTP để không ai nhảy cóc bước 1)
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string email, string otp, string newPassword, string confirmPassword)
    {
        if (!CheckOtp(email, otp, out var error))
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction("ResetPassword", new { email });
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "Mật khẩu xác nhận không khớp!";
            ViewBag.Email = email;
            ViewBag.Otp = otp;
            ViewBag.Verified = true;
            return View("ResetPassword");
        }

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account == null)
        {
            TempData["ErrorMessage"] = "Tài khoản không tồn tại.";
            return RedirectToAction("ForgotPassword");
        }

        account.Password = newPassword;
        await _context.SaveChangesAsync();
        _cache.Remove(OtpKey(email)); // OTP dùng một lần

        TempData["SuccessMessage"] = "Đổi mật khẩu thành công, hãy đăng nhập.";
        return RedirectToAction("Login");
    }

    private static string OtpKey(string email) => "otp:" + (email ?? "").Trim().ToLower();

    private bool CheckOtp(string email, string otp, out string error)
    {
        error = "";
        if (!_cache.TryGetValue(OtpKey(email), out OtpEntry? entry) || entry == null)
        {
            error = "OTP đã hết hạn, vui lòng gửi lại mã mới.";
            return false;
        }
        if (entry.Attempts >= 5)
        {
            _cache.Remove(OtpKey(email));
            error = "Bạn nhập sai quá 5 lần, vui lòng gửi lại mã mới.";
            return false;
        }
        if (entry.Code != otp)
        {
            entry.Attempts++;
            error = "OTP không đúng.";
            return false;
        }
        return true;
    }

    private async Task SendOtpMailAsync(string toEmail, string otp)
    {
        var senderEmail = _config["OtpMail:SenderEmail"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config["OtpMail:SenderName"] ?? "E-Greeting", senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Mã OTP đặt lại mật khẩu - E-Greeting";
        message.Body = new BodyBuilder
        {
            HtmlBody = $@"
            <div style='font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:25px;border:1px solid #e2e8f0;border-radius:12px;text-align:center'>
                <h2 style='color:#0d6efd;margin:0 0 10px'>E-Greeting</h2>
                <p style='color:#334155'>Mã OTP đặt lại mật khẩu của bạn là:</p>
                <div style='font-size:36px;font-weight:bold;letter-spacing:10px;color:#0d6efd;margin:20px 0'>{otp}</div>
                <p style='color:#64748b;font-size:14px'>Mã có hiệu lực trong 5 phút. Không chia sẻ mã này cho bất kỳ ai.</p>
            </div>"
        }.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(
            _config["OtpMail:SmtpServer"],
            int.Parse(_config["OtpMail:Port"] ?? "587"),
            SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(senderEmail, _config["OtpMail:Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}

public class OtpEntry
{
    public string Code { get; set; } = "";
    public int Attempts { get; set; }
}