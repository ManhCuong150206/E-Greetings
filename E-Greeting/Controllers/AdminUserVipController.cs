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
            // =========================================================================
            // LAZY AUTO-UPDATE: EXPIRE OUTDATED SUBSCRIPTIONS & PENDING PAYMENTS IN DB
            // =========================================================================
            var nowDateTime = DateTime.Now.Date;

            // Fetch active subscriptions and expire them in memory/DB safely
            var allActiveSubs = await _context.Subscriptions
                .Where(s => s.IsActive == true)
                .ToListAsync();

            var expiredSubs = allActiveSubs
                .Where(s => ConvertToDateTime((object?)s.EndDate) < nowDateTime)
                .ToList();

            if (expiredSubs.Any())
            {
                foreach (var sub in expiredSubs)
                {
                    sub.IsActive = false;
                    _context.Entry(sub).Property(x => x.IsActive).IsModified = true;
                }
            }

            // Fetch UNPAID payments and expire those older than 3 days
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            var unpaidPayments = await _context.Payments
                .Where(p => p.Status == "UNPAID")
                .ToListAsync();

            var expiredPayments = unpaidPayments
                .Where(p => ConvertToDateTime((object?)p.PaymentDate) < threeDaysAgo)
                .ToList();

            if (expiredPayments.Any())
            {
                foreach (var pay in expiredPayments)
                {
                    pay.Status = "EXPIRED";
                    _context.Entry(pay).Property(x => x.Status).IsModified = true;
                }
            }

            if (expiredSubs.Any() || expiredPayments.Any())
            {
                await _context.SaveChangesAsync();
            }
            // =========================================================================

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

            // Sort DESCENDING by Id (NEWEST USER FIRST)
            var rawUsers = await usersQuery
                .OrderByDescending(u => u.Id)
                .Skip((pageUser - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Retrieve Active Subscriptions valid for today
            var rawActiveSubscriptions = await _context.Subscriptions
                .Include(s => s.Service)
                .Where(s => s.IsActive == true)
                .OrderByDescending(s => s.Id)
                .ToListAsync();

            var activeSubscriptions = rawActiveSubscriptions
                .Where(s => ConvertToDateTime((object?)s.EndDate) >= nowDateTime)
                .ToList();

            var sentCardCounts = await _context.SentCards
                .Where(s => s.AccountId.HasValue)
                .GroupBy(s => s.AccountId!.Value)
                .Select(g => new { AccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.AccountId, g => g.Count);

            model.Users = rawUsers.Select(u => {
                var activeSub = activeSubscriptions.FirstOrDefault(s => s.AccountId == u.Id);
                DateTime? endDate = activeSub != null ? ConvertToNullableDateTime((object?)activeSub.EndDate) : null;

                return new AccountUserDto
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role,
                    Status = u.Status,
                    CurrentVipPackage = activeSub?.Service != null ? activeSub.Service.Name : "Free Package",
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

            // Sort DESCENDING by Id (NEWEST PAYMENT TRANSACTION FIRST)
            var rawTxList = await txQuery
                .OrderByDescending(p => p.Id)
                .Skip((pageTx - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            model.Transactions = rawTxList.Select(p => {
                DateTime? subStart = p.Subscription != null ? ConvertToNullableDateTime((object?)p.Subscription.StartDate) : null;
                DateTime? subEnd = p.Subscription != null ? ConvertToNullableDateTime((object?)p.Subscription.EndDate) : null;

                int subIdVal = p.SubscriptionId;
                int accountIdVal = p.Subscription != null ? p.Subscription.AccountId : 0;
                int serviceIdVal = (p.Subscription != null && p.Subscription.ServiceId.HasValue) ? p.Subscription.ServiceId.Value : 0;

                return new PaymentTransactionDto
                {
                    PaymentId = p.Id,
                    SubscriptionId = subIdVal,
                    AccountId = accountIdVal,
                    UserName = p.Subscription?.Account != null ? p.Subscription.Account.Name : "N/A",
                    UserEmail = p.Subscription?.Account != null ? p.Subscription.Account.Email : "N/A",
                    ServiceId = serviceIdVal,
                    ServiceName = p.Subscription?.Service != null ? p.Subscription.Service.Name : "Paid Package",
                    Amount = p.Amount,
                    PaymentDate = ConvertToDateTime((object?)p.PaymentDate),
                    VipStartDate = subStart,
                    VipEndDate = subEnd,
                    PaymentMethod = p.PaymentMethod ?? "SePay/QR",
                    Status = p.Status ?? "UNPAID"
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

        // Edit User Information Action (EMAIL IS READ-ONLY TO PROTECT TRANSACTION HISTORY INTEGRITY)
        [HttpPost("Admin/EditUser")]
        public async Task<IActionResult> EditUser(int id, string name)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                account.Name = name;
                _context.Accounts.Update(account);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Updated user profile successfully!";
            }
            return RedirectToAction(nameof(Users), new { tab = "users" });
        }

        // Edit Service Package Action (STRICT VALIDATION: PRICE CANNOT BE NEGATIVE)
        [HttpPost("Admin/EditService")]
        public async Task<IActionResult> EditService(int id, decimal price)
        {
            // Validate non-negative price
            if (price < 0)
            {
                TempData["ErrorMessage"] = "Package price cannot be negative!";
                return RedirectToAction(nameof(Users), new { tab = "payments" });
            }

            var service = await _context.Services.FindAsync(id);
            if (service != null)
            {
                // Only update pricing, keeping package name and core features locked
                service.Price = price;

                _context.Services.Update(service);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Updated price for package '{service.Name}' successfully!";
            }
            return RedirectToAction(nameof(Users), new { tab = "payments" });
        }

        // Manual Payment Approval Action (STRICT CHECK: PREVENT OVERWRITING SAME ACTIVE PACKAGE)
        [HttpPost("Admin/ApprovePayment")]
        public async Task<IActionResult> ApprovePayment(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Service)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment != null && payment.Subscription != null)
            {
                int accountId = payment.Subscription.AccountId;
                int targetServiceId = payment.Subscription.ServiceId ?? 0;
                var nowDateTime = DateTime.Now.Date;

                // Fetch active subscriptions for this user
                var userActiveSubs = await _context.Subscriptions
                    .Include(s => s.Service)
                    .Where(s => s.AccountId == accountId && s.IsActive == true)
                    .ToListAsync();

                var currentActiveSub = userActiveSubs
                    .FirstOrDefault(s => ConvertToDateTime((object?)s.EndDate) >= nowDateTime);

                // Check duplicate approval for the same package level
                if (currentActiveSub != null && currentActiveSub.ServiceId == targetServiceId)
                {
                    DateTime activeEndDate = ConvertToDateTime((object?)currentActiveSub.EndDate);
                    TempData["ErrorMessage"] = $"Cannot approve payment #{paymentId}! User already has an active subscription (ID: #{currentActiveSub.Id}, Package: {currentActiveSub.Service?.Name}) valid until {activeEndDate:dd/MM/yyyy}. Direct approval is blocked to prevent duplicate charges or time overlaps.";
                    return RedirectToAction(nameof(Users), new { tab = "payments" });
                }

                // If upgrading package, deactivate old active subscription
                if (currentActiveSub != null && currentActiveSub.ServiceId != targetServiceId)
                {
                    currentActiveSub.IsActive = false;
                    _context.Entry(currentActiveSub).Property(x => x.IsActive).IsModified = true;
                }

                // Approve payment and activate new subscription
                payment.Status = "PAID";
                payment.Subscription.IsActive = true;

                SetDynamicDate(payment.Subscription, "StartDate", DateTime.Now);
                SetDynamicDate(payment.Subscription, "EndDate", DateTime.Now.AddMonths(1));

                _context.Entry(payment).State = EntityState.Modified;
                _context.Entry(payment.Subscription).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Approved payment successfully for invoice #{paymentId}!";
            }
            return RedirectToAction(nameof(Users), new { tab = "payments" });
        }

        private static void SetDynamicDate(object obj, string propName, DateTime dateTimeValue)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return;

            if (prop.PropertyType == typeof(DateOnly) || prop.PropertyType == typeof(DateOnly?))
            {
                prop.SetValue(obj, DateOnly.FromDateTime(dateTimeValue));
            }
            else
            {
                prop.SetValue(obj, dateTimeValue);
            }
        }

        private static DateTime ConvertToDateTime(object? dateObj)
        {
            if (dateObj == null) return DateTime.MinValue;
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