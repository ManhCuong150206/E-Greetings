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
        // Truy vấn danh sách mẫu thiệp từ CSDL
        var templates = await _context.Templates.ToListAsync();
        return View(templates);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}