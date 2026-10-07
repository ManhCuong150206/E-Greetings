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
            ViewBag.Error = "Please enter your feedback.";
            return View("Index");
        }

        if (content.Length > 1000)
        {
            ViewBag.Error = "Feedback must be at most 1000 characters.";
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

        TempData["SuccessMessage"] = "Thank you for your feedback!";
        return RedirectToAction("Index");
    }
}