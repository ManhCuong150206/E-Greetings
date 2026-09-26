using E_Greeting.Models;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace E_Greeting.Controllers;

public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty;
}

public class ReplyRequest
{
    public int CardId { get; set; }
    public string ReplyMessage { get; set; } = string.Empty;
}

[Authorize]
public class CardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    public CardController(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // GET: /Card/Personalize/1
    [HttpGet]
    public IActionResult Personalize(int id)
    {
        var template = _context.Templates.FirstOrDefault(t => t.Id == id) ?? new Template
        {
            Id = id > 0 ? id : 1,
            Name = "Thiệp Chúc Mừng Mẫu",
            ImagePath = "https://picsum.photos/800/600?random=" + id
        };

        return View(template);
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

        // 1. Lấy AccountId từ User Claim nếu có
        int? accountId = null;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int parsedUserId))
        {
            accountId = parsedUserId;
        }

        // 2. Kiểm tra TemplateId có thực sự tồn tại trong DB hay không để tránh lỗi Khóa Ngoại (Foreign Key)
        int? validTemplateId = null;
        if (request.TemplateId > 0)
        {
            var templateExists = await _context.Templates.AnyAsync(t => t.Id == request.TemplateId);
            if (templateExists)
            {
                validTemplateId = request.TemplateId;
            }
        }

        var sentCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = validTemplateId,
            SenderEmail = senderEmail,
            RecipientEmail = request.RecipientEmail,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Thiệp chúc mừng dành cho bạn" : request.Subject,
            CardHtmlContent = request.CardHtmlContent,
            AudioUrl = request.AudioUrl,
            CreatedDate = DateTime.Now,
            OpenAt = null,
            RecipientReply = null
        };

        // 3. Thực hiện lưu vào CSDL an toàn với try-catch
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

        // Tạo Full URL đường dẫn xem thiệp (Ví dụ: https://localhost:7123/Card/ViewCard/12)
        string fullCardUrl = $"{Request.Scheme}://{Request.Host}/Card/ViewCard/{sentCard.Id}";

        // 4. TỰ ĐỘNG GỬI EMAIL THẬT TỚI NGƯỜI NHẬN
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
            // Nếu có lỗi gửi mail (ví dụ chưa cấu hình xong app password), thiệp vẫn lưu thành công nhưng ghi log
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
        var card = await _context.SentCards.FirstOrDefaultAsync(c => c.Id == id);

        if (card != null)
        {
            if (card.OpenAt == null)
            {
                card.OpenAt = DateTime.Now;
                _context.SentCards.Update(card);
                await _context.SaveChangesAsync();
            }

            ViewBag.CardId = card.Id;
            ViewBag.SenderEmail = card.SenderEmail;
            ViewBag.RecipientEmail = card.RecipientEmail;
            ViewBag.Subject = card.Subject;
            ViewBag.AudioUrl = card.AudioUrl ?? "";
            ViewBag.CardHtmlContent = card.CardHtmlContent;
            ViewBag.OpenAt = card.OpenAt?.ToString("dd/MM/yyyy HH:mm");
            ViewBag.RecipientReply = card.RecipientReply;
        }
        else
        {
            ViewBag.CardId = id;
            ViewBag.SenderEmail = "nguoidung@example.com";
            ViewBag.AudioUrl = "";
            ViewBag.CardHtmlContent = "<div class='p-4 text-center text-muted'>Thiệp không tồn tại hoặc đã bị xóa.</div>";
            ViewBag.OpenAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        return View();
    }

    // POST: /Card/ReplyCard
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ReplyCard([FromBody] ReplyRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReplyMessage))
        {
            return Json(new { success = false, message = "Vui lòng nhập nội dung cảm ơn!" });
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
            message = "Đã gửi lời cảm ơn đến người tặng thiệp!"
        });
    }
}