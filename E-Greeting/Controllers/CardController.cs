using E_Greeting.Models;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Greeting.Controllers;

// --- DTO Requests ---
public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string? AudioPath { get; set; }
    public string? SubscriptionId { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty; // Chứa HTML canvas
    public string? Message { get; set; } // Chứa JSON form thô
}

public class ReplyRequest
{
    public int CardId { get; set; }
    public string ReplyMessage { get; set; } = string.Empty;
}

// --- Controller ---
[Authorize]
public class CardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env;

    public CardController(AppDbContext context, IEmailService emailService, IWebHostEnvironment env)
    {
        _context = context;
        _emailService = emailService;
        _env = env;
    }

    // GET: /Card/Personalize/1
    [HttpGet]
    public async Task<IActionResult> Personalize(int id)
    {
        var template = await _context.Templates.FirstOrDefaultAsync(t => t.Id == id);
        return template == null ? NotFound("Không tìm thấy mẫu thiệp này!") : View(template);
    }

    // POST: /Card/SaveCardConfig
    [HttpPost]
    public async Task<IActionResult> SaveCardConfig([FromBody] SaveCardRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return Json(new { success = false, message = "Vui lòng nhập Email người nhận hợp lệ!" });
        }

        var senderEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "nguoidung@domain.com";

        int? accountId = null;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int parsedUserId))
        {
            accountId = parsedUserId;
        }

        int? validTemplateId = null;
        Template? template = null;
        if (request.TemplateId > 0)
        {
            template = await _context.Templates.FirstOrDefaultAsync(t => t.Id == request.TemplateId);
            if (template != null) validTemplateId = request.TemplateId;
        }

        // Xử lý lưu ảnh Base64 nếu user có upload ảnh mới
        string? savedImagePath = SaveBase64Image(request.ImagePath);

        if (string.IsNullOrEmpty(savedImagePath))
        {
            savedImagePath = !string.IsNullOrEmpty(request.ImagePath) ? request.ImagePath : template?.ImagePath;
        }

        var sentCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = validTemplateId,
            RecipientEmail = request.RecipientEmail.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Thiệp chúc mừng" : request.Subject,
            CardHtmlContent = request.CardHtmlContent, // Gán đúng HTML Canvas
            Message = request.Message,                 // Gán đúng JSON Form thô
            ImagePath = savedImagePath,
            AudioPath = request.AudioPath,
            SentAt = DateTime.Now
        };

        try
        {
            _context.SentCards.Add(sentCard);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return Json(new { success = false, message = "Lỗi lưu CSDL: " + innerMsg });
        }

        string fullCardUrl = $"{Request.Scheme}://{Request.Host}/Card/ViewCard/{sentCard.Id}";

        try
        {
            await _emailService.SendCardEmailAsync(
                sentCard.RecipientEmail,
                sentCard.Subject,
                fullCardUrl,
                senderEmail
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi gửi email: " + ex.Message);
        }

        return Json(new
        {
            success = true,
            message = "Lưu thiệp & Gửi Email thành công!",
            cardId = sentCard.Id,
            cardUrl = fullCardUrl
        });
    }

    // GET: /Card/ViewCard/1024
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ViewCard(int id)
    {
        var card = await _context.SentCards
            .Include(c => c.Account)
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (card == null)
        {
            return NotFound("Thiệp không tồn tại hoặc đã bị xóa.");
        }

        if (card.OpenAt == null)
        {
            card.OpenAt = DateTime.Now;
            _context.SentCards.Update(card);
            await _context.SaveChangesAsync();
        }

        if (string.IsNullOrEmpty(card.ImagePath) && card.Template != null)
        {
            card.ImagePath = card.Template.ImagePath;
        }

        return View(card);
    }

    // POST: /Card/ReplyCard
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ReplyCard([FromBody] ReplyRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReplyMessage))
        {
            return Json(new { success = false, message = "Vui lòng nhập nội dung phản hồi!" });
        }

        var card = await _context.SentCards.FirstOrDefaultAsync(c => c.Id == request.CardId);
        if (card == null)
        {
            return Json(new { success = false, message = "Không tìm thấy thông tin thiệp!" });
        }

        card.RecipientReply = request.ReplyMessage;
        _context.SentCards.Update(card);
        await _context.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "Đã gửi phản hồi thành công!"
        });
    }

    private string? SaveBase64Image(string? base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String)) return null;
        if (!base64String.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)) return base64String;

        try
        {
            var parts = base64String.Split(',');
            if (parts.Length < 2) return null;

            byte[] imageBytes = Convert.FromBase64String(parts[1]);
            string extension = ".png";
            if (parts[0].Contains("jpeg", StringComparison.OrdinalIgnoreCase) || parts[0].Contains("jpg", StringComparison.OrdinalIgnoreCase))
                extension = ".jpg";

            string folderPath = Path.Combine(_env.WebRootPath, "uploads", "img_sentcards");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = $"card_{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(folderPath, fileName);
            System.IO.File.WriteAllBytes(filePath, imageBytes);

            return $"/uploads/img_sentcards/{fileName}";
        }
        catch
        {
            return null;
        }
    }
}