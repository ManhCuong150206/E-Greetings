using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; 

[Authorize(Roles = "Admin,ADMIN")] // CHẶN CHẶT CHẼ: Chỉ Admin mới được truy cập Controller này
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public AdminController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // 1. Dashboard Mặc Định
    public IActionResult Dashboard()
    {
        return View();
    }

    // 2. Danh sách Category
    public IActionResult Index()
    {
        var categories = _context.Categories
            .Include(c => c.Templates)
            .ToList();
        return View(categories);
    }

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

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var category = _context.Categories.Find(id);
        if (category == null) return NotFound();
        return View(category);
    }

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

    // --- QUẢN LÝ TEMPLATE THEO CATEGORY ---

    // Danh sách Template trực thuộc 1 Category
    public IActionResult Templates(int categoryId)
    {
        var category = _context.Categories
            .Include(c => c.Templates)
            .FirstOrDefault(c => c.Id == categoryId);

        if (category == null) return NotFound();

        ViewBag.CategoryName = category.Name;
        ViewBag.CategoryId = category.Id;
        return View(category.Templates.ToList());
    }

    // GET: Form Thêm mới Template (Dùng Canvas Editor)
    [HttpGet]
    public IActionResult CreateTemplate(int categoryId)
    {
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = _context.Categories.ToList();
        return View();
    }

    // POST: Lưu Template Mới (Xử lý 2 Ảnh + Ghép JSON config_data)
    [HttpPost]
    public async Task<IActionResult> CreateTemplate(
        int categoryId,
        string name,
        bool isFree,
        IFormFile imageFile,       // 1. Ảnh Bìa Thô (Background Canvas)
        IFormFile? thumbnailFile,  // 2. Ảnh Bìa Card (Preview Thumbnail)
        string configData)
    {
        if (string.IsNullOrWhiteSpace(name) || imageFile == null)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập tên Template và Upload Ảnh Bìa Thô!";
            return RedirectToAction(nameof(CreateTemplate), new { categoryId });
        }

        string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "templates");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        // 1. Upload Ảnh Bìa Thô
        string bgFileName = Guid.NewGuid().ToString() + "_bg_" + Path.GetFileName(imageFile.FileName);
        string bgFilePath = Path.Combine(uploadsFolder, bgFileName);
        using (var fileStream = new FileStream(bgFilePath, FileMode.Create))
        {
            await imageFile.CopyToAsync(fileStream);
        }
        string bgUrl = "/uploads/templates/" + bgFileName;

        // 2. Upload Ảnh Bìa Card Preview (Nếu không upload sẽ lấy mặc định là Ảnh Bìa Thô)
        string thumbUrl = bgUrl;
        if (thumbnailFile != null && thumbnailFile.Length > 0)
        {
            string thumbFileName = Guid.NewGuid().ToString() + "_thumb_" + Path.GetFileName(thumbnailFile.FileName);
            string thumbFilePath = Path.Combine(uploadsFolder, thumbFileName);
            using (var fileStream = new FileStream(thumbFilePath, FileMode.Create))
            {
                await thumbnailFile.CopyToAsync(fileStream);
            }
            thumbUrl = "/uploads/templates/" + thumbFileName;
        }

        // 3. Đóng gói JSON bao gồm preview_thumbnail + danh sách elements chữ
        string finalConfigJson = configData;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configData) ? "{\"elements\":[]}" : configData);
            var root = doc.RootElement;

            var configDict = new System.Collections.Generic.Dictionary<string, object>
            {
                ["preview_thumbnail"] = thumbUrl
            };

            if (root.TryGetProperty("elements", out var elementsProp))
            {
                configDict["elements"] = elementsProp;
            }
            else
            {
                configDict["elements"] = new object[] { };
            }

            finalConfigJson = JsonSerializer.Serialize(configDict);
        }
        catch
        {
            // Fallback nếu JSON parse có sự cố
            finalConfigJson = $"{{\"preview_thumbnail\":\"{thumbUrl}\",\"elements\":[]}}";
        }

        // 4. Lưu vào SQL Server
        var template = new Template
        {
            Name = name,
            CategoryId = categoryId,
            ImagePath = bgUrl,
            ConfigData = finalConfigJson,
            IsFree = isFree
        };

        _context.Templates.Add(template);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Tạo mẫu Template mới thành công!";
        return RedirectToAction(nameof(Templates), new { categoryId });
    }

    // Các Tab mở rộng khác
    public IActionResult Users() => View();
    public IActionResult Feedbacks() => View();
    public IActionResult SentLogs() => View();
}