using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace E_Greeting.Controllers
{
    [Authorize(Roles = "Admin,ADMIN")]
    public class AdminCategoryController : Controller
    {
        private readonly AppDbContext _context;

        public AdminCategoryController(AppDbContext context)
        {
            _context = context;
        }

        // --- CATEGORY MANAGEMENT ---
        [HttpGet("Admin/Categories")]
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
                .OrderBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.SearchName = searchName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View("~/Views/Admin/Categories.cshtml", categories);
        }

        [HttpPost("Admin/Create")]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Category added successfully!";
                return RedirectToAction(nameof(Categories));
            }
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet("Admin/Edit/{id}")]
        public IActionResult Edit(int id)
        {
            var category = _context.Categories.Find(id);
            if (category == null) return NotFound();
            return View("~/Views/Admin/Edit.cshtml", category);
        }

        [HttpPost("Admin/Edit/{id}")]
        public IActionResult Edit(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Update(category);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Category updated successfully!";
                return RedirectToAction(nameof(Categories));
            }
            return View("~/Views/Admin/Edit.cshtml", category);
        }

        [HttpPost("Admin/Delete/{id}")]
        public IActionResult Delete(int id)
        {
            var category = _context.Categories.Find(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Category deleted successfully!";
            }
            return RedirectToAction(nameof(Categories));
        }
    }
}