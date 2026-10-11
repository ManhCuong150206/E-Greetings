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
    public class AdminSentHistoryController : Controller
    {
        private readonly AppDbContext _context;

        public AdminSentHistoryController(AppDbContext context)
        {
            _context = context;
        }

        // LEVEL 1: LIST OF USERS WHO HAVE SENT CARDS (SORTED DESCENDING BY ID)
        [HttpGet("Admin/SentLogs")]
        [HttpGet("AdminSentHistory/SentLogs")]
        public async Task<IActionResult> SentLogs(string? search, int page = 1)
        {
            int pageSize = 10; // Maximum 10 rows per page

            var query = _context.Accounts
                .Where(a => _context.SentCards.Any(s => s.AccountId == a.Id))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Name.Contains(search) || a.Email.Contains(search));
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // Sort Descending by Real DB Id so newest users appear first
            var usersList = await query
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var sentCounts = await _context.SentCards
                .Where(s => s.AccountId.HasValue)
                .GroupBy(s => s.AccountId!.Value)
                .Select(g => new { AccountId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.AccountId, g => g.Count);

            ViewBag.SentCounts = sentCounts;
            ViewBag.Search = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Admin/SentLogs.cshtml", usersList);
        }

        // AJAX API: FETCH USERS WITH SENT LOGS JSON
        [HttpGet("Admin/GetSentLogsUsersJson")]
        public async Task<IActionResult> GetSentLogsUsersJson(string? search, int page = 1)
        {
            int pageSize = 10;

            var query = _context.Accounts
                .Where(a => _context.SentCards.Any(s => s.AccountId == a.Id))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.Name.Contains(search) || a.Email.Contains(search));
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var usersList = await query
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    email = a.Email,
                    sentCount = _context.SentCards.Count(s => s.AccountId == a.Id)
                })
                .ToListAsync();

            return Json(new
            {
                users = usersList,
                currentPage = page,
                totalPages = totalPages
            });
        }

        // LEVEL 2: DETAILED SENT CARDS LIST FOR A SPECIFIC USER (SORTED DESCENDING BY ID)
        [HttpGet("Admin/UserSentCards/{accountId}")]
        public async Task<IActionResult> UserSentCards(int accountId, int page = 1)
        {
            var account = await _context.Accounts.FindAsync(accountId);
            if (account == null) return NotFound();

            int pageSize = 10; // Maximum 10 rows per page
            var query = _context.SentCards
                .Include(s => s.Template)
                .Where(s => s.AccountId == accountId)
                .AsQueryable();

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            // Sort Descending by Real DB Id so newest sent cards appear first
            var cards = await query
                .OrderByDescending(s => s.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.AccountName = account.Name;
            ViewBag.AccountEmail = account.Email;
            ViewBag.AccountId = accountId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Admin/UserSentCards.cshtml", cards);
        }
    }
}