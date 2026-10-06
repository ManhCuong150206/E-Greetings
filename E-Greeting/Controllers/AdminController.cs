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
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // --- TAB 1: SYSTEM OVERVIEW DASHBOARD & REVENUE REPORTING ---
        [HttpGet("Admin/Dashboard")]
        [HttpGet("Admin")]
        public async Task<IActionResult> Dashboard()
        {
            var model = new DashboardViewModel();

            // 1. Overview Statistics
            model.TotalTemplates = await _context.Templates.CountAsync();
            model.TotalCategories = await _context.Categories.CountAsync();
            model.TotalUsers = await _context.Accounts.CountAsync(a => a.Role != "ADMIN" && a.Role != "Admin");
            model.TotalSentCards = await _context.SentCards.CountAsync();

            // 2. Calculate TOTAL REVENUE from all packages
            model.TotalRevenue = await _context.Payments
                .Where(p => p.Status == "PAID" || p.Status == "Success" || p.Status == "Completed")
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            // 3. Detailed Statistics for Each Service Package
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

            // 4. Retrieve Top 5 Recent VIP Transactions
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
                UserName = p.Subscription?.Account != null ? p.Subscription.Account.Name : "VIP Customer",
                PackageName = p.Subscription?.Service != null ? p.Subscription.Service.Name : "Paid Package",
                Amount = p.Amount,
                PaymentDate = ConvertToDateTime(p.PaymentDate),
                Status = p.Status
            }).ToList();

            // 5. Monthly Revenue Statistics for the Current Year
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

            return View("~/Views/Admin/Dashboard.cshtml", model);
        }

        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        private static DateTime ConvertToDateTime(object dateObj)
        {
            if (dateObj is DateTime dt) return dt;
            if (dateObj is DateOnly dOnly) return dOnly.ToDateTime(TimeOnly.MinValue);
            return Convert.ToDateTime(dateObj);
        }
    }
}