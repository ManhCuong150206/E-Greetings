using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_Greeting.Controllers;

public class FeedbackController : Controller
{
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SendFeedback(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            ViewBag.Error = "Vui lòng nhập nội dung phản hồi!";
            return View("Index");
        }

        string? accountId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi phản hồi! Ý kiến của bạn đã được ghi nhận.";
        return RedirectToAction("Index");
    }
}