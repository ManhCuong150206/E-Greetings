using E_Greeting.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace E_Greeting.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    // Inject AppDbContext vào Controller
    public HomeController(AppDbContext context) => _context = context;

    // GET: / (Trang chủ)
    public async Task<IActionResult> Index()
    {
        // 1. Chặn Admin vào giao diện User: Nếu tài khoản đang đăng nhập là Admin -> Chuyển thẳng tới Admin Dashboard
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin") || User.IsInRole("ADMIN"))
            {
                return RedirectToAction("Dashboard", "Admin"); // Đổi thành "Index" nếu action mặc định của AdminController là Index
            }
        }

        // 2. Nếu là User bình thường hoặc Khách chưa đăng nhập -> Hiển thị danh sách Template
        var templates = await _context.Templates
            .Where(t => t.IsActive == true)
            .ToListAsync();

        return View(templates);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}