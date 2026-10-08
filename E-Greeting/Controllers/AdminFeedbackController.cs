using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace E_Greeting.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class AdminFeedbackController : Controller
    {
        private readonly AppDbContext _context;

        public AdminFeedbackController(AppDbContext context)
        {
            _context = context;
        }

        // LEVEL 1: LIST OF USERS WHO HAVE SUBMITTED FEEDBACK
        [HttpGet("Admin/Feedbacks")]
        [HttpGet("AdminFeedback/Feedbacks")]
        public async Task<IActionResult> Feedbacks(string? search, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Accounts
                .Where(a => _context.Feedbacks.Any(f => f.AccountId == a.Id))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Name.Contains(search) || a.Email.Contains(search));
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // SORT DESCENDING BY ID (NEWEST USER FIRST)
            var usersList = await query
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var feedbackCounts = await _context.Feedbacks
                .GroupBy(f => f.AccountId)
                .Select(g => new { AccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.AccountId, g => g.Count);

            ViewBag.FeedbackCounts = feedbackCounts;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Admin/Feedbacks.cshtml", usersList);
        }

        // LEVEL 2: DETAILED FEEDBACK LIST FOR A SPECIFIC USER
        [HttpGet("Admin/UserFeedbacks/{accountId}")]
        public async Task<IActionResult> UserFeedbacks(int accountId, int page = 1)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return NotFound();

            int pageSize = 10;
            var query = _context.Feedbacks
                .Where(f => f.AccountId == accountId)
                .AsQueryable();

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // SORT DESCENDING BY ID (NEWEST FEEDBACK FIRST)
            var feedbacks = await query
                .OrderByDescending(f => f.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.AccountName = account.Name;
            ViewBag.AccountEmail = account.Email;
            ViewBag.AccountId = account.Id;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Admin/UserFeedbacks.cshtml", feedbacks);
        }
    }
}