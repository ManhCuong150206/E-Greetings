using Microsoft.AspNetCore.Mvc;
using E_Greeting.Models;

public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    // Lấy danh sách Category
    public IActionResult Index()
    {
        var categories = _context.Categories.ToList();
        return View(categories);
    }

    // Thêm mới Category
    [HttpPost]
    public IActionResult Create(Category category)
    {
        if (ModelState.IsValid)
        {
            _context.Categories.Add(category);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // 1. Hiển thị Form Sửa Category
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var category = _context.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // 2. Xử lý Cập nhật Category vào CSDL
    [HttpPost]
    public IActionResult Edit(Category category)
    {
        if (ModelState.IsValid)
        {
            _context.Categories.Update(category);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // 3. Xử lý Xóa Category
    [HttpPost]
    public IActionResult Delete(int id)
    {
        var category = _context.Categories.Find(id);
        if (category != null)
        {
            _context.Categories.Remove(category);
            _context.SaveChanges();
        }
        return RedirectToAction(nameof(Index));
    }
}