using E_Greeting.Models;
using E_Greeting.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

public class AccountController : Controller
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

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
        new(ClaimTypes.Name, account.Name),
        new(ClaimTypes.Role, account.Role)
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
    public IActionResult Register()
    {
        return View();
    }

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
}