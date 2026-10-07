using E_Greeting.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace E_Greeting.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context) => _context = context;

    // GET: / (Home page)
    public async Task<IActionResult> Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin") || User.IsInRole("ADMIN"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
        }

        var templates = await _context.Templates.Include(t => t.Category).ToListAsync();

        List<int> wishlistedCardIds = new();
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            string currentUserName = User.Identity.Name ?? "";
            wishlistedCardIds = await _context.Wishlists
                .Include(w => w.Account)
                .Where(w => w.Account != null && (w.Account.Name == currentUserName || w.Account.Email == currentUserName))
                .Select(w => w.TemplateId)
                .ToListAsync();
        }

        ViewBag.WishlistCardIds = wishlistedCardIds;
        return View(templates);
    }

    // AJAX Action to handle Toggle Wishlist (Add / Remove)
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleWishlist(int templateId)
    {
        try
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Please log in to use this feature!" });
            }

            string currentUserName = User.Identity.Name ?? "";

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Name == currentUserName || a.Email == currentUserName);

            if (account == null)
            {
                return Json(new { success = false, message = "Account information not found!" });
            }

            var existingItem = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.AccountId == account.Id && w.TemplateId == templateId);

            bool isAdded = false;
            if (existingItem != null)
            {
                _context.Wishlists.Remove(existingItem);
            }
            else
            {
                _context.Wishlists.Add(new Wishlist
                {
                    AccountId = account.Id,
                    TemplateId = templateId
                });
                isAdded = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isAdded = isAdded });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Server error: " + ex.Message });
        }
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}