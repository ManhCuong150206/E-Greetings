using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public class AdminTemplateController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminTemplateController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // --- MANAGE TEMPLATES BY CATEGORY ---
        [HttpGet("Admin/Templates")]
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

            return View("~/Views/Admin/Templates.cshtml", templates);
        }

        [HttpPost("Admin/ToggleTemplateStatusAjax")]
        public async Task<IActionResult> ToggleTemplateStatusAjax(int id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null) return Json(new { success = false, message = "Template not found!" });

            template.IsActive = !template.IsActive;
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();

            return Json(new { success = true, isActive = template.IsActive });
        }

        [HttpGet("Admin/CreateTemplate")]
        public IActionResult CreateTemplate(int categoryId)
        {
            ViewBag.CategoryId = categoryId;
            ViewBag.Categories = _context.Categories.ToList();
            return View("~/Views/Admin/CreateTemplate.cshtml");
        }

        [HttpPost("Admin/CreateTemplate")]
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
                TempData["ErrorMessage"] = "Please enter Template Name and upload Raw Cover Image!";
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

            TempData["SuccessMessage"] = "Created new Template successfully!";
            return RedirectToAction(nameof(Templates), new { categoryId });
        }

        [HttpGet("Admin/EditTemplate")]
        public IActionResult EditTemplate(int id)
        {
            var template = _context.Templates.FirstOrDefault(t => t.Id == id);
            if (template == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();
            return View("~/Views/Admin/EditTemplate.cshtml", template);
        }

        [HttpPost("Admin/EditTemplate")]
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

            TempData["SuccessMessage"] = "Updated Template successfully!";
            return RedirectToAction(nameof(Templates), new { categoryId = template.CategoryId });
        }
    }
}