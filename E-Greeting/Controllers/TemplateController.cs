using E_Greeting.Service.Categories;
using E_Greeting.Service.Templates;
using E_Greeting.ViewModels.Templates;
using Microsoft.AspNetCore.Mvc;
using static System.Net.WebRequestMethods;

namespace E_Greeting.Controllers
{
    [Route("template")]
    public class TemplateController(ITemplateService templateService, ICategoryService cateService) : Controller
    {
        [Route("")]
        [HttpGet("all-templates")]
        public async Task<IActionResult> Index(TemplateFilter filter)
        {
            var templates = await templateService.GetTemplates(filter);
            var cate = await cateService.GetCategories();
            ViewBag.Category = cate.ToList();
            return View(templates); 
        }

        // ==========================================
        // ACTION CHO TRANG CHI TIẾT THIỆP (VIEW DETAIL)
        // Đường dẫn truy cập: /template/details/1 hoặc /template/details?id=1
        // ==========================================
        [HttpGet("details/{id?}")]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
            {
                return NotFound();
            }

            // 1. Lấy thông tin chi tiết của mẫu thiệp theo Id
            var template = await templateService.GetTemplateById(id);
            if (template == null)
            {
                return NotFound();
            }

            // 2. Lấy danh sách thiệp liên quan (cùng danh mục) để hiển thị ở mục "Có thể bạn cũng thích"
            var filter = new TemplateFilter 
            { 
                CategoryId = template.CategoryId, 
                PageSize = 4 
            };
            var relatedTemplates = await templateService.GetTemplates(filter);
            
            // Loại bỏ mẫu thiệp hiện tại khỏi danh sách gợi ý
            ViewBag.RelatedTemplates = relatedTemplates.Templates
                .Where(t => t.Id != id)
                .Take(4)
                .ToList();

            return View(template);
        }
    }
}
