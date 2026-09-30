using E_Greeting.Service.Categories;
using E_Greeting.Service.Templates;
using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers
{
    [Route("template")]
    public class TemplateController(ITemplateService templateService, ICategoryService cateService) : Controller
    {
        [Route("")]
        [HttpGet("all-templates")]
        public async Task<IActionResult> Index()
        {
            var templates = await templateService.GetTemplates();
            var cate = await cateService.GetCategories();
            ViewBag.Category = cate.ToList();
            return View(templates); 
        }
    }
}
