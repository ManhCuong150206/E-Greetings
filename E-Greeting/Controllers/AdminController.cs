using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using E_Greeting.Models;
using System.Linq;

public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    // Hiển thị danh sách Category & Template
    public IActionResult Index()
    {
        var categories = _context.Categories
            .Include(c => c.Templates)
            .ToList();

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
            TempData["SuccessMessage"] = "Thêm danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(nameof(Index), _context.Categories.Include(c => c.Templates).ToList());
    }

    // Hiển thị Form Sửa Category
    [HttpGet]
    public IActionResult Edit(int id)
    {
        var category = _context.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

    // Cập nhật Category
    [HttpPost]
    public IActionResult Edit(Category category)
    {
        if (ModelState.IsValid)
        {
            _context.Categories.Update(category);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    // Xóa Category
    [HttpPost]
    public IActionResult Delete(int id)
    {
        var category = _context.Categories.Find(id);
        if (category != null)
        {
            _context.Categories.Remove(category);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Xóa danh mục thành công!";
        }
        return RedirectToAction(nameof(Index));
    }
}