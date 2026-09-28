using E_Greeting.Models;
using E_Greeting.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Greeting.Controllers;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly AppDbContext _context;

    public AccountController(IAccountService accountService, AppDbContext context)
    {
        _accountService = accountService;
        _context = context;
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
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

        // Tới đây chắc chắn result == LoginResult.Success, account không null
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Name ?? account.Email),
            new(ClaimTypes.Email, account.Email), // Bổ sung Claim Email để tự động dùng khi gửi thiệp
            new(ClaimTypes.Role, account.Role.Trim().ToLower() == "admin" ? "Admin" : account.Role.Trim())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return account.Role.ToLower() == "admin"
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction("Index", "Home");
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

    // GET: /Account/Profile - Lấy thông tin Account truyền trực tiếp sang View
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return RedirectToAction("Login");

        var account = await _context.Accounts.FindAsync(userId);
        return account == null ? RedirectToAction("Login") : View(account);
    }

    // POST: /Account/UpdateProfile - Cập nhật Họ và tên
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

    // POST: /Account/ChangePassword - Kiểm tra mật khẩu cũ & Cập nhật mật khẩu mới
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
}