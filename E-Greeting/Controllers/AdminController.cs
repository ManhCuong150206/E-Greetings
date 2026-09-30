using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;

namespace E_Greeting.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // --- DASHBOARD TỔNG QUAN HỆ THỐNG ---
        public IActionResult Dashboard()
        {
            // Thống kê dữ liệu thực tế để đưa lên giao diện Dashboard.cshtml
            ViewBag.TotalTemplates = _context.Templates.Count();
            ViewBag.TotalCategories = _context.Categories.Count();
            ViewBag.TotalUsers = _context.Accounts.Count(a => a.Role != "ADMIN" && a.Role != "Admin");

            // Lấy 5 mẫu thiệp mới nhất để hiển thị nhanh (nếu cần)
            ViewBag.RecentTemplates = _context.Templates
                .Include(t => t.Category)
                .OrderByDescending(t => t.Id)
                .Take(5)
                .ToList();

            return View();
        }

        // --- QUẢN LÝ DANH MỤC (CATEGORIES) ---

        public IActionResult Categories()
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

        [HttpGet]
        public IActionResult CreateTemplate(int categoryId)
        {
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        // POST: Lưu Template Mới (ImagePath = Thumbnail | ConfigData = BG + Elements)
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

            // 1. Upload Ảnh Bìa Thô (Background Canvas)
            string bgFileName = Guid.NewGuid().ToString() + "_bg_" + Path.GetFileName(imageFile.FileName);
            string bgFilePath = Path.Combine(uploadsFolder, bgFileName);
            using (var fileStream = new FileStream(bgFilePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            string bgUrl = "/uploads/templates/" + bgFileName;

            // 2. Upload Ảnh Thumbnail Card Preview
            string thumbUrl = bgUrl; // Nếu không có thumbnail riêng thì dùng luôn ảnh thô
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

            // 3. Đóng gói JSON ConfigData (Lưu background_image + elements)
            string finalConfigJson;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configData) ? "{\"elements\":[]}" : configData);
                var root = doc.RootElement;

                var configDict = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["background_image"] = bgUrl
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
                finalConfigJson = $"{{\"background_image\":\"{bgUrl}\",\"elements\":[]}}";
            }

            // 4. Lưu vào CSDL
            var template = new Template
            {
                Name = name,
                CategoryId = categoryId,
                ImagePath = thumbUrl,         // Cột image_path lưu Ảnh Thumbnail
                ConfigData = finalConfigJson, // Cột config_data lưu Ảnh thô + Canvas JSON
                IsFree = isFree,
                IsActive = true
            };

            _context.Templates.Add(template);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tạo mẫu Template mới thành công!";
            return RedirectToAction(nameof(Templates), new { categoryId });
        }

        [HttpGet]
        public IActionResult EditTemplate(int id)
        {
            var template = _context.Templates.FirstOrDefault(t => t.Id == id);
            if (template == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View(template);
        }

        [HttpPost]
        public async Task<IActionResult> EditTemplate(
            int id,
            string name,
            int categoryId,
            bool isFree,
            bool isActive,
            IFormFile? imageFile,
            IFormFile? thumbnailFile,
            string configData)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return NotFound();

            template.Name = name;
            template.CategoryId = categoryId;
            template.IsFree = isFree;
            template.IsActive = isActive;

            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "templates");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            // 1. Trích xuất background_image cũ từ JSON config_data
            string oldBgUrl = "";
            if (!string.IsNullOrWhiteSpace(template.ConfigData))
            {
                try
                {
                    using var doc = JsonDocument.Parse(template.ConfigData);
                    if (doc.RootElement.TryGetProperty("background_image", out var bgProp))
                    {
                        oldBgUrl = bgProp.GetString() ?? "";
                    }
                }
                catch { }
            }

            // 2. Xử lý Ảnh Bìa Thô mới (Upload ảnh mới -> xóa ảnh cũ)
            string bgUrl = string.IsNullOrEmpty(oldBgUrl) ? template.ImagePath : oldBgUrl;
            if (imageFile != null && imageFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(oldBgUrl))
                {
                    string oldPath = Path.Combine(_environment.WebRootPath, oldBgUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                string bgFileName = Guid.NewGuid().ToString() + "_bg_" + Path.GetFileName(imageFile.FileName);
                string bgFilePath = Path.Combine(uploadsFolder, bgFileName);
                using (var stream = new FileStream(bgFilePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }
                bgUrl = "/uploads/templates/" + bgFileName;
            }

            // 3. Xử lý Ảnh Thumbnail mới (Lưu trực tiếp vào template.ImagePath)
            string thumbUrl = template.ImagePath;
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(template.ImagePath) && template.ImagePath != oldBgUrl)
                {
                    string oldThumbPath = Path.Combine(_environment.WebRootPath, template.ImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldThumbPath)) System.IO.File.Delete(oldThumbPath);
                }

                string thumbFileName = Guid.NewGuid().ToString() + "_thumb_" + Path.GetFileName(thumbnailFile.FileName);
                string thumbFilePath = Path.Combine(uploadsFolder, thumbFileName);
                using (var stream = new FileStream(thumbFilePath, FileMode.Create))
                {
                    await thumbnailFile.CopyToAsync(stream);
                }
                thumbUrl = "/uploads/templates/" + thumbFileName;
            }
            template.ImagePath = thumbUrl; // Cập nhật lại ImagePath

            // 4. Đóng gói lại JSON ConfigData
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configData) ? "{\"elements\":[]}" : configData);
                var root = doc.RootElement;

                var configDict = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["background_image"] = bgUrl
                };

                if (root.TryGetProperty("elements", out var elementsProp))
                {
                    configDict["elements"] = elementsProp;
                }
                else
                {
                    configDict["elements"] = new object[] { };
                }

                template.ConfigData = JsonSerializer.Serialize(configDict);
            }
            catch { }

            _context.Templates.Update(template);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật mẫu Template thành công!";
            return RedirectToAction(nameof(Templates), new { categoryId = template.CategoryId });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTemplateStatus(int id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template != null)
            {
                template.IsActive = !template.IsActive;
                _context.Templates.Update(template);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = template.IsActive ? "Đã HIỆN mẫu thiệp lên danh sách User!" : "Đã ẨN mẫu thiệp khỏi danh sách User!";
            }
            return RedirectToAction(nameof(Templates), new { categoryId = template?.CategoryId });
        }

        public IActionResult Users() => View();
        public IActionResult Feedbacks() => View();
        public IActionResult SentLogs() => View();
    }
}