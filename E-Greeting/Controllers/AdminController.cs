using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // --- TAB 1: DASHBOARD TỔNG QUAN HỆ THỐNG & BÁO CÁO DOANH THU ---
        public async Task<IActionResult> Dashboard()
        {
            var model = new DashboardViewModel();

            // 1. Thống kê số liệu tổng quan
            model.TotalTemplates = await _context.Templates.CountAsync();
            model.TotalCategories = await _context.Categories.CountAsync();
            model.TotalUsers = await _context.Accounts.CountAsync(a => a.Role != "ADMIN" && a.Role != "Admin");
            model.TotalSentCards = await _context.SentCards.CountAsync();

            // 2. Tính TỔNG DOANH THU tất cả các gói (chỉ tính trạng thái thanh toán thành công "PAID")
            model.TotalRevenue = await _context.Payments
                .Where(p => p.Status == "PAID" || p.Status == "Success" || p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            // 3. Thống kê chi tiết từng Gói Dịch Vụ (Service)
            var allServices = await _context.Services.ToListAsync();
            var paidPaymentsList = await _context.Payments
                .Include(p => p.Subscription)
                .Where(p => p.Status == "PAID" || p.Status == "Success" || p.Status == "Completed")
                .ToListAsync();

            model.ServiceStats = allServices.Select(s => {
                var paymentsOfService = paidPaymentsList.Where(p => p.Subscription != null && p.Subscription.ServiceId == s.Id);
                return new ServiceStatDto
                {
                    ServiceId = s.Id,
                    ServiceName = s.Name,
                    Price = s.Price,
                    TotalSubscribers = paymentsOfService.Count(),
                    TotalRevenue = paymentsOfService.Sum(p => p.Amount)
                };
            }).ToList();

            // 4. Lấy 5 giao dịch VIP mới nhất cho bảng Lịch sử giao dịch
            var recentPaymentsList = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Account)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Service)
                .OrderByDescending(p => p.Id)
                .Take(5)
                .ToListAsync();

            model.RecentPayments = recentPaymentsList.Select(p => new RecentPaymentDto
            {
                PaymentId = p.Id,
                UserName = p.Subscription?.Account != null ? p.Subscription.Account.Name : "Khách hàng VIP",
                PackageName = p.Subscription?.Service != null ? p.Subscription.Service.Name : "Gói Trả Phí",
                Amount = p.Amount,
                PaymentDate = p.PaymentDate.ToDateTime(TimeOnly.MinValue), // Chuyển DateOnly sang DateTime an toàn
                Status = p.Status
            }).ToList();

            // 5. Thống kê doanh thu 12 tháng năm hiện tại cho Biểu đồ (Chart.js)
            int currentYear = DateTime.Now.Year;

            var monthlyData = paidPaymentsList
                .Where(p => p.PaymentDate.Year == currentYear)
                .GroupBy(p => p.PaymentDate.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.Amount) });

            foreach (var item in monthlyData)
            {
                if (item.Month >= 1 && item.Month <= 12)
                {
                    model.MonthlyRevenue[item.Month - 1] = item.Total;
                }
            }

            return View(model);
        }

        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        // --- TAB 2: QUẢN LÝ DANH MỤC (CATEGORIES) + SEARCH & PAGING ---

        public IActionResult Categories(string? searchName, int page = 1)
        {
            int pageSize = 10;
            var query = _context.Categories.Include(c => c.Templates).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(c => c.Name.Contains(searchName));
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var categories = query
                .OrderByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchName = searchName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

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
                return RedirectToAction(nameof(Categories));
            }
            return RedirectToAction(nameof(Categories));
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
                return RedirectToAction(nameof(Categories));
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
            return RedirectToAction(nameof(Categories));
        }

        // --- QUẢN LÝ TEMPLATE THEO CATEGORY + SEARCH, FILTER & PAGING ---

        public IActionResult Templates(int categoryId, string? searchName, string? isFree, string? isActive, int page = 1)
        {
            var category = _context.Categories.Find(categoryId);
            if (category == null) return NotFound();

            int pageSize = 8;
            var query = _context.Templates.Where(t => t.CategoryId == categoryId).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchName))
            {
                query = query.Where(t => t.Name.Contains(searchName));
            }

            if (!string.IsNullOrWhiteSpace(isFree))
            {
                if (isFree == "true") query = query.Where(t => t.IsFree == true);
                else if (isFree == "false") query = query.Where(t => t.IsFree == false);
            }

            if (!string.IsNullOrWhiteSpace(isActive))
            {
                if (isActive == "true") query = query.Where(t => t.IsActive == true);
                else if (isActive == "false") query = query.Where(t => t.IsActive == false);
            }

            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var templates = query
                .OrderByDescending(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CategoryName = category.Name;
            ViewBag.CategoryId = category.Id;

            ViewBag.SearchName = searchName;
            ViewBag.IsFree = isFree;
            ViewBag.IsActive = isActive;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(templates);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTemplateStatusAjax(int id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return Json(new { success = false, message = "Không tìm thấy Template!" });

            template.IsActive = !template.IsActive;
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();

            return Json(new { success = true, isActive = template.IsActive });
        }

        [HttpGet]
        public IActionResult CreateTemplate(int categoryId)
        {
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTemplate(
            int categoryId,
            string name,
            bool isFree,
            IFormFile imageFile,
            IFormFile? thumbnailFile,
            string configData)
        {
            if (string.IsNullOrWhiteSpace(name) || imageFile == null)
            {
                TempData["ErrorMessage"] = "Vui lòng nhập tên Template và Upload Ảnh Bìa Thô!";
                return RedirectToAction(nameof(CreateTemplate), new { categoryId });
            }

            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "templates");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string bgFileName = Guid.NewGuid().ToString() + "_bg_" + Path.GetFileName(imageFile.FileName);
            string bgFilePath = Path.Combine(uploadsFolder, bgFileName);
            using (var fileStream = new FileStream(bgFilePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }
            string bgUrl = "/uploads/templates/" + bgFileName;

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

            string finalConfigJson;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configData) ? "{\"elements\":[]}" : configData);
                var root = doc.RootElement;

                var configDict = new Dictionary<string, object>
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

            var template = new Template
            {
                Name = name,
                CategoryId = categoryId,
                ImagePath = thumbUrl,
                ConfigData = finalConfigJson,
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
            template.ImagePath = thumbUrl;

            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configData) ? "{\"elements\":[]}" : configData);
                var root = doc.RootElement;

                var configDict = new Dictionary<string, object>
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

        public IActionResult Users() => View();
        public IActionResult Feedbacks() => View();
        public IActionResult SentLogs() => View();
    }
}