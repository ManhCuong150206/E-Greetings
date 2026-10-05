using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Greeting.Controllers;

[Authorize]
public class FeedbackController : Controller
{
    private readonly AppDbContext _context;

    public FeedbackController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SendFeedback(string content)
    {
        content = (content ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(content))
        {
            ViewBag.Error = "Vui lòng nhập nội dung phản hồi.";
            return View("Index");
        }

        if (content.Length > 1000)
        {
            ViewBag.Error = "Nội dung phản hồi tối đa 1000 ký tự.";
            return View("Index");
        }

        int accountId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        _context.Feedbacks.Add(new Feedback
        {
            AccountId = accountId,
            Content = content,
            CreatedAt = DateTime.Now
        });
        _context.SaveChanges();

        TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi phản hồi!";
        return RedirectToAction("Index");
    }
}