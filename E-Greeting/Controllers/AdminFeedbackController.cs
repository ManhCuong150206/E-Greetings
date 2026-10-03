using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("Admin/Feedbacks")]
        [HttpGet("AdminFeedback/Feedbacks")]
        public IActionResult Feedbacks()
        {
            return View("~/Views/Admin/Feedbacks.cshtml");
        }
    }
}