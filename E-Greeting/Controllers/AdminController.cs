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

            // 2. Tính TỔNG DOANH THU tất cả các gói
            model.TotalRevenue = await _context.Payments
                .Where(p => p.Status == "PAID" || p.Status == "Success" || p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            // 3. Thống kê chi tiết từng Gói Dịch Vụ
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

            // 4. Lấy 5 giao dịch VIP mới nhất
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
                PaymentDate = ConvertToDateTime(p.PaymentDate),
                Status = p.Status
            }).ToList();

            // 5. Thống kê doanh thu 12 tháng năm hiện tại
            int currentYear = DateTime.Now.Year;

            var monthlyData = paidPaymentsList
                .Where(p => ConvertToDateTime(p.PaymentDate).Year == currentYear)
                .GroupBy(p => ConvertToDateTime(p.PaymentDate).Month)
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

        // --- TAB 2: QUẢN LÝ DANH MỤC (CATEGORIES) ---

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

        // --- QUẢN LÝ TEMPLATE THEO CATEGORY ---

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

        // --- TAB 3: QUẢN LÝ USERS & GÓI VIP / LỊCH SỬ GIAO DỊCH ---

        [HttpGet]
        public async Task<IActionResult> Users(string? searchUser = null, string? searchTx = null, int? serviceId = null, string? status = null, string tab = "users")
        {
            var model = new UserManagementViewModel
            {
                ActiveTab = tab,
                SearchUser = searchUser,
                SearchTransaction = searchTx,
                SelectedServiceId = serviceId,
                SelectedStatus = status
            };

            // 1. Lấy danh sách Gói Dịch Vụ
            model.Services = await _context.Services.ToListAsync();

            // 2. Lấy danh sách Accounts (chỉ lấy Role != ADMIN)
            var usersQuery = _context.Accounts
                .Where(a => a.Role != "ADMIN" && a.Role != "Admin")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchUser))
            {
                usersQuery = usersQuery.Where(u => u.Name.Contains(searchUser) || u.Email.Contains(searchUser));
            }

            var rawUsers = await usersQuery.OrderByDescending(u => u.Id).ToListAsync();
            var subscriptions = await _context.Subscriptions.Include(s => s.Service).Where(s => s.IsActive == true).ToListAsync();

            var sentCardCounts = await _context.SentCards
                .Where(s => s.AccountId.HasValue)
                .GroupBy(s => s.AccountId!.Value)
                .Select(g => new { AccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.AccountId, g => g.Count);

            model.Users = rawUsers.Select(u => {
                var activeSub = subscriptions.FirstOrDefault(s => s.AccountId == u.Id);

                DateTime? endDate = activeSub != null ? ConvertToNullableDateTime((object)activeSub.EndDate) : null;

                return new AccountUserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    CurrentVipPackage = activeSub?.Service != null ? activeSub.Service.Name : "Miễn phí",
                    VipEndDate = endDate,
                    SentCardsCount = sentCardCounts.ContainsKey(u.Id) ? sentCardCounts[u.Id] : 0
                };
            }).ToList();

            // 3. Lấy danh sách Lịch Sử Giao Dịch (Payment)
            var txQuery = _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Account)
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Service)
                .AsQueryable();

            if (serviceId.HasValue && serviceId.Value > 0)
            {
                txQuery = txQuery.Where(p => p.Subscription != null && p.Subscription.ServiceId == serviceId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                txQuery = txQuery.Where(p => p.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(searchTx))
            {
                txQuery = txQuery.Where(p => (p.Subscription != null && p.Subscription.Account != null &&
                    (p.Subscription.Account.Name.Contains(searchTx) || p.Subscription.Account.Email.Contains(searchTx))) || p.Id.ToString().Contains(searchTx));
            }

            var rawTxList = await txQuery.OrderByDescending(p => p.Id).ToListAsync();

            model.Transactions = rawTxList.Select(p => {
                DateTime? subStart = p.Subscription != null ? ConvertToNullableDateTime((object?)p.Subscription.StartDate) : null;
                DateTime? subEnd = p.Subscription != null ? ConvertToNullableDateTime((object?)p.Subscription.EndDate) : null;

                return new PaymentTransactionDto
                {
                    PaymentId = p.Id,
                    SubscriptionId = p.SubscriptionId,
                    AccountId = p.Subscription?.AccountId ?? 0,
                    UserName = p.Subscription?.Account != null ? p.Subscription.Account.Name : "N/A",
                    UserEmail = p.Subscription?.Account != null ? p.Subscription.Account.Email : "N/A",
                    ServiceId = p.Subscription?.ServiceId ?? 0,
                    ServiceName = p.Subscription?.Service != null ? p.Subscription.Service.Name : "Gói Trả Phí",
                    Amount = p.Amount,
                    PaymentDate = ConvertToDateTime(p.PaymentDate),
                    VipStartDate = subStart,
                    VipEndDate = subEnd,
                    PaymentMethod = p.PaymentMethod ?? "VNPAY/Thẻ",
                    Status = p.Status ?? ""
                };
            }).ToList();

            return View(model);
        }

        // Action khóa/mở khóa tài khoản
        [HttpPost]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                account.Status = !account.Status;
                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã {(account.Status ? "kích hoạt" : "khóa")} tài khoản {account.Name}!";
            }
            return RedirectToAction(nameof(Users), new { tab = "users" });
        }

        // Action chỉnh sửa thông tin User
        [HttpPost]
        public async Task<IActionResult> EditUser(int id, string name, string email)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                account.Name = name;
                account.Email = email;
                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin tài khoản thành công!";
            }
            return RedirectToAction(nameof(Users), new { tab = "users" });
        }

        // Action cập nhật Gói dịch vụ
        [HttpPost]
        public async Task<IActionResult> EditService(int id, string name, decimal price, string features)
        {
            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                service.Name = name;
                service.Price = price;
                service.Features = features;
                _context.Services.Update(service);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin gói VIP thành công!";
            }
            return RedirectToAction(nameof(Users), new { tab = "payments" });
        }

        // Action duyệt thủ công trạng thái thanh toán
        [HttpPost]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _context.Payments.Include(p => p.Subscription).FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment != null)
            {
                payment.Status = "PAID";
                _context.Payments.Update(payment);

                if (payment.Subscription != null)
                {
                    payment.Subscription.IsActive = true;
                    _context.Subscriptions.Update(payment.Subscription);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã duyệt thanh toán thành công cho đơn #{paymentId}!";
            }
            return RedirectToAction(nameof(Users), new { tab = "payments" });
        }

        public IActionResult Feedbacks() => View();
        public IActionResult SentLogs() => View();

        // --- CÁC HÀM HELPER ÉP KIỂU NGÀY THÁNG CHUẨN XÁC NGUYÊN BẢN ---
        private static DateTime ConvertToDateTime(object dateObj)
        {
            if (dateObj is DateTime dt) return dt;
            if (dateObj is DateOnly dOnly) return dOnly.ToDateTime(TimeOnly.MinValue);
            return Convert.ToDateTime(dateObj);
        }

        private static DateTime? ConvertToNullableDateTime(object? dateObj)
        {
            if (dateObj == null) return null;
            if (dateObj is DateTime dt) return dt;
            if (dateObj is DateOnly dOnly) return dOnly.ToDateTime(TimeOnly.MinValue);
            return Convert.ToDateTime(dateObj);
        }
    }
}