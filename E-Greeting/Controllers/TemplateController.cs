using E_Greeting.Service.Templates;
using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers
{
    [Route("template")]
    public class TemplateController(ITemplateService templateService) : Controller
    {
        [Route("")]
        [HttpGet("all-templates")]
        public async Task<IActionResult> Index()
        {
            var templates = await templateService.GetTemplates();
            return View(templates);
        }
    }
}
