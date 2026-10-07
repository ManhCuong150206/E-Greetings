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
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
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
    public IActionResult Register()
    {
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View();
    }
    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

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
    // SECTION 4: PERSONAL INFORMATION MANAGEMENT & PASSWORD CHANGE
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

        TempData["SuccessMessage"] = "Personal information updated successfully!";
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
            TempData["ErrorMessage"] = "The new password and the confirmation password do not match!";
            return RedirectToAction("Profile");
        }

        var account = await _context.Accounts.FindAsync(userId);
        if (account == null) return RedirectToAction("Login");

        if (account.Password != currentPassword)
        {
            TempData["ErrorMessage"] = "The current password is incorrect!";
            return RedirectToAction("Profile");
        }

        account.Password = newPassword;
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Password changed successfully!";
        return RedirectToAction("Profile");
    }

    // ==========================================
    // FORGOT PASSWORD (OTP VIA GMAIL)
    // ==========================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View();
    }


    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        email = (email ?? "").Trim();

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account == null)
        {
            TempData["ErrorMessage"] = "Account does not exist.";
            return RedirectToAction("ForgotPassword");
        }

        // Generate a 6-digit OTP, store for 5 minutes
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        _cache.Set(OtpKey(email), new OtpEntry { Code = otp }, TimeSpan.FromMinutes(5));

        try
        {
            await SendOtpMailAsync(email, otp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Failed to send OTP: " + ex.Message + " | INNER: " + ex.InnerException?.Message);
            TempData["ErrorMessage"] = "Unable to send email, please try again later.";
            return RedirectToAction("ForgotPassword");
        }

        TempData["SuccessMessage"] = "The OTP has been sent to your email (valid for 5 minutes).";
        return RedirectToAction("ResetPassword", new { email });
    }

    [HttpGet]
    public IActionResult ResetPassword(string email)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewBag.Email = email;
        ViewBag.Verified = false;
        return View();
    }

    // Step 1: verify OTP
    [HttpPost]
    public IActionResult VerifyOtp(string email, string otp)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
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

    // Step 2: change password (re-verify the OTP so nobody can skip step 1)
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string email, string otp, string newPassword, string confirmPassword)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        if (!CheckOtp(email, otp, out var error))
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction("ResetPassword", new { email });
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
        {
            TempData["ErrorMessage"] = "The confirmation password does not match!";
            ViewBag.Email = email;
            ViewBag.Otp = otp;
            ViewBag.Verified = true;
            return View("ResetPassword");
        }

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account == null)
        {
            TempData["ErrorMessage"] = "Account does not exist.";
            return RedirectToAction("ForgotPassword");
        }

        account.Password = newPassword;
        await _context.SaveChangesAsync();
        _cache.Remove(OtpKey(email)); // OTP is single-use

        TempData["SuccessMessage"] = "Password changed successfully, please login.";
        return RedirectToAction("Login");
    }

    private static string OtpKey(string email) => "otp:" + (email ?? "").Trim().ToLower();

    private bool CheckOtp(string email, string otp, out string error)
    {
        error = "";
        if (!_cache.TryGetValue(OtpKey(email), out OtpEntry? entry) || entry == null)
        {
            error = "The OTP has expired, please request a new code.";
            return false;
        }
        if (entry.Attempts >= 5)
        {
            _cache.Remove(OtpKey(email));
            error = "You entered the wrong code more than 5 times, please request a new code.";
            return false;
        }
        if (entry.Code != otp)
        {
            entry.Attempts++;
            error = "Incorrect OTP.";
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
        message.Subject = "Password reset OTP - E-Greeting";
        message.Body = new BodyBuilder
        {
            HtmlBody = $@"
            <div style='font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:25px;border:1px solid #e2e8f0;border-radius:12px;text-align:center'>
                <h2 style='color:#0d6efd;margin:0 0 10px'>E-Greeting</h2>
                <p style='color:#334155'>Your password reset OTP     is:</p>
                <div style='font-size:36px;font-weight:bold;letter-spacing:10px;color:#0d6efd;margin:20px 0'>{otp}</div>
                <p style='color:#64748b;font-size:14px'>This code is valid for 5 minutes. Do not share this code with anyone.</p>
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