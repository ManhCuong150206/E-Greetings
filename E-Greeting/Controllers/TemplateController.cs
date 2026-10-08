using E_Greeting.Models;
using E_Greeting.Service.Categories;
using E_Greeting.Service.Templates;
using E_Greeting.ViewModels.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace E_Greeting.Controllers
{
    [Route("template")]
    public class TemplateController : Controller
    {
        private readonly ITemplateService _templateService;
        private readonly ICategoryService _cateService;
        private readonly AppDbContext _context;

        public TemplateController(ITemplateService templateService, ICategoryService cateService, AppDbContext context)
        {
            _templateService = templateService;
            _cateService = cateService;
            _context = context;
        }

        [Route("")]
        [HttpGet("all-templates")]
        public async Task<IActionResult> Index(TemplateFilter filter)
        {
            var templates = await _templateService.GetTemplates(filter);
            var cate = await _cateService.GetCategories();
            ViewBag.Category = cate.ToList();
            return View(templates);
        }

        // ACTION FOR E-CARD DETAILS PAGE
        [HttpGet("details/{id?}")]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return NotFound();

            var template = await _templateService.GetTemplateById(id);
            if (template == null) return NotFound();

            // 1. Check whether current account is Premium VIP (ServiceId == 2, IsActive == true, EndDate >= today)
            bool isWishlisted = false;
            bool isUserVip = false;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                string currentUserName = User.Identity.Name ?? "";
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Name == currentUserName || a.Email == currentUserName);

                if (account != null)
                {
                    // Check Wishlist
                    isWishlisted = await _context.Wishlists.AnyAsync(w => w.AccountId == account.Id && w.TemplateId == id);

                    // STRICT CHECK: User must have an active Premium VIP plan (ServiceId == 2, IsActive == true, EndDate >= today)
                    var today = DateOnly.FromDateTime(DateTime.Now);
                    isUserVip = await _context.Subscriptions.AnyAsync(s =>
                        s.AccountId == account.Id &&
                        s.ServiceId == 2 &&        // Must be Service 2 (Premium VIP)
                        s.IsActive == true &&      // Must be paid & activated
                        s.EndDate >= today);       // Must not be expired
                }
            }

            ViewBag.IsWishlisted = isWishlisted;
            ViewBag.IsUserVip = isUserVip;

            // 2. Fetch related card templates list
            var filter = new TemplateFilter
            {
                CategoryId = template.CategoryId,
                PageSize = 4
            };
            var relatedTemplates = await _templateService.GetTemplates(filter);

            ViewBag.RelatedTemplates = relatedTemplates.Templates
                .Where(t => t.Id != id)
                .Take(4)
                .ToList();

            return View(template);
        }
    }
}