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

        // --- MANAGE USERS & VIP PACKAGES / TRANSACTION HISTORY ---
        [HttpGet("Admin/Users")]
        public async Task<IActionResult> Users(
            string? searchUser = null,
            string? searchTx = null,
            int? serviceId = null,
            string? status = null,
            string tab = "users",
            int pageUser = 1,
            int pageTx = 1)
        {
            var model = new UserManagementViewModel
            {
                ActiveTab = tab,
                SearchUser = searchUser,
                SearchTransaction = searchTx,
                SelectedServiceId = serviceId,
                SelectedStatus = status
            };

            int pageSize = 10; // Maximum 10 rows per page

            // 1. Retrieve Services List
            model.Services = await _context.Services.ToListAsync();

            // 2. Process Users Query (Exclude ADMIN role)
            var usersQuery = _context.Accounts
                .Where(a => a.Role != "ADMIN" && a.Role != "Admin")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchUser))
            {
                usersQuery = usersQuery.Where(u => u.Name.Contains(searchUser) || u.Email.Contains(searchUser));
            }

            int totalUsersCount = await usersQuery.CountAsync();
            int totalUserPages = (int)Math.Ceiling((double)totalUsersCount / pageSize);
            pageUser = Math.Max(1, Math.Min(pageUser, Math.Max(1, totalUserPages)));

            // Sort Ascending by Id (Real DB Id from smallest to largest)
            var rawUsers = await usersQuery
                .OrderBy(u => u.Id)
                .Skip((pageUser - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Retrieve Active Subscriptions
            var subscriptions = await _context.Subscriptions
                .Include(s => s.Service)
                .Where(s => s.IsActive == true)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

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
                    CurrentVipPackage = activeSub?.Service != null ? activeSub.Service.Name : "Free",
                    VipEndDate = endDate,
                    SentCardsCount = sentCardCounts.ContainsKey(u.Id) ? sentCardCounts[u.Id] : 0
                };
            }).ToList();

            ViewBag.CurrentPageUser = pageUser;
            ViewBag.TotalPagesUser = totalUserPages;

            // 3. Process Transaction History (Payment) Query
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

            int totalTxCount = await txQuery.CountAsync();
            int totalTxPages = (int)Math.Ceiling((double)totalTxCount / pageSize);
            pageTx = Math.Max(1, Math.Min(pageTx, Math.Max(1, totalTxPages)));

            // Sort Ascending by Id (Real DB Id from smallest to largest)
            var rawTxList = await txQuery
                .OrderBy(p => p.Id)
                .Skip((pageTx - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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
                    ServiceName = p.Subscription?.Service != null ? p.Subscription.Service.Name : "Paid Package",
                    Amount = p.Amount,
                    PaymentDate = ConvertToDateTime(p.PaymentDate),
                    VipStartDate = subStart,
                    VipEndDate = subEnd,
                    PaymentMethod = p.PaymentMethod ?? "VNPAY/Card",
                    Status = p.Status ?? ""
                };
            }).ToList();

            ViewBag.CurrentPageTx = pageTx;
            ViewBag.TotalPagesTx = totalTxPages;

            return View("~/Views/Admin/Users.cshtml", model);
        }

        // Lock / Unlock User Account Action
        [HttpPost("Admin/ToggleUserStatus")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                account.Status = !account.Status;
                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"{(account.Status ? "Activated" : "Locked")} account {account.Name} successfully!";
            }
            return RedirectToAction(nameof(Users), new { tab = "users" });
        }

        // Edit User Information Action
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
                TempData["SuccessMessage"] = "Updated user information successfully!";
            }
            return RedirectToAction(nameof(Users), new { tab = "users" });
        }

        // Edit Service Package Action
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
                TempData["SuccessMessage"] = "Updated VIP service package successfully!";
            }
            return RedirectToAction(nameof(Users), new { tab = "payments" });
        }

        // Manual Payment Approval Action
        [HttpPost("Admin/ApprovePayment")]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment != null)
            {
                payment.Status = "PAID";
                _context.Payments.Update(payment);

                if (payment.Subscription != null)
                {
                    int accountId = payment.Subscription.AccountId;

                    // Deactivate older active subscriptions for this user
                    var oldActiveSubs = await _context.Subscriptions
                        .Where(s => s.AccountId == accountId && s.IsActive == true && s.Id != payment.SubscriptionId)
                        .ToListAsync();

                    foreach (var oldSub in oldActiveSubs)
                    {
                        oldSub.IsActive = false;
                        _context.Subscriptions.Update(oldSub);
                    }

                    // Activate current subscription
                    payment.Subscription.IsActive = true;
                    _context.Subscriptions.Update(payment.Subscription);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Approved payment successfully for invoice #{paymentId}!";
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