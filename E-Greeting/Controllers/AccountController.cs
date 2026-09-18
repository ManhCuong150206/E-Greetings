using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers;

public class AccountController : Controller
{
    // GET: /Account/Login
    public IActionResult Login() => View();

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string email, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ Email và Mật khẩu!";
            return View();
        }

        // Tạm thời giả lập đăng nhập thành công (sẽ kết nối Entity Framework sau)
        TempData["SuccessMessage"] = "Đăng nhập thành công! Chào mừng bạn quay trở lại.";
        return RedirectToAction("Index", "Home");
    }

    // GET: /Account/Register
    public IActionResult Register() => View();

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(string fullName, string email, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ các thông tin bắt buộc!";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Mật khẩu xác nhận không trùng khớp!";
            return View();
        }

        // Tạm thời giả lập đăng ký thành công
        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
        return RedirectToAction("Login");
    }

    // POST: /Account/Logout
    [HttpPost]
    public IActionResult Logout() => RedirectToAction("Index", "Home");
}