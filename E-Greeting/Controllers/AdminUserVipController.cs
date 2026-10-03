using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace E_Greeting.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class AdminUserVipController : Controller
    {
        private readonly AppDbContext _context;

        public AdminUserVipController(AppDbContext context)
        {
            _context = context;
        }

        // --- QUẢN LÝ USERS & GÓI VIP / LỊCH SỬ GIAO DỊCH ---
        [HttpGet("Admin/Users")]
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

            return View("~/Views/Admin/Users.cshtml", model);
        }

        // Action khóa/mở khóa tài khoản
        [HttpPost("Admin/ToggleUserStatus")]
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
        [HttpPost("Admin/EditUser")]
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
        [HttpPost("Admin/EditService")]
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
        [HttpPost("Admin/ApprovePayment")]
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