using E_Greeting.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace E_Greeting.Controllers;

// DTO hứng toàn bộ dữ liệu giao diện từ FE
public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty; // Lưu toàn bộ HTML thiệp (chữ, font, màu, sticker, vị trí kéo thả)
}

public class ReplyRequest
{
    public int CardId { get; set; }
    public string ReplyMessage { get; set; } = string.Empty;
}

public class CardController : Controller
{
    private readonly AppDbContext _context;

    // Bộ nhớ tạm lưu thiệp trên Server
    private static readonly ConcurrentDictionary<int, SaveCardRequest> _cardMemoryStore = new();

    public CardController(AppDbContext context) => _context = context;

    // GET: /Card/Personalize/1
    [HttpGet]
    public IActionResult Personalize(int id)
    {
        var template = new Template
        {
            Id = id > 0 ? id : 1,
            Name = "Thiệp Chúc Mừng Mẫu",
            ImagePath = "https://picsum.photos/800/600?random=" + id
        };

        return View(template);
    }

    // POST: Lưu cấu hình thiệp
    [HttpPost]
    public IActionResult SaveCardConfig([FromBody] SaveCardRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return Json(new { success = false, message = "Vui lòng nhập Email người nhận hợp lệ!" });
        }

        int newCardId = new Random().Next(1000, 9999);
        _cardMemoryStore[newCardId] = request;

        string viewUrl = $"/Card/ViewCard/{newCardId}";

        return Json(new
        {
            success = true,
            message = "Lưu & gửi thiệp thành công!",
            cardId = newCardId,
            cardUrl = viewUrl
        });
    }

    // GET: /Card/ViewCard/1024
    [HttpGet]
    public IActionResult ViewCard(int id)
    {
        if (_cardMemoryStore.TryGetValue(id, out var card))
        {
            ViewBag.CardId = id;
            ViewBag.SenderEmail = card.RecipientEmail;
            ViewBag.AudioUrl = card.AudioUrl ?? "";
            ViewBag.CardHtmlContent = card.CardHtmlContent; // Đưa nguyên vẹn HTML sang View
            ViewBag.OpenAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }
        else
        {
            ViewBag.CardId = id;
            ViewBag.SenderEmail = "nguoidung@example.com";
            ViewBag.AudioUrl = "";
            ViewBag.CardHtmlContent = "<div class='p-4 text-center text-muted'>Thiệp không tồn tại hoặc đã hết hạn.</div>";
            ViewBag.OpenAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        return View();
    }

    // POST: Lời cảm ơn từ người nhận
    [HttpPost]
    public IActionResult ReplyCard([FromBody] ReplyRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReplyMessage))
        {
            return Json(new { success = false, message = "Vui lòng nhập nội dung cảm ơn!" });
        }

        return Json(new
        {
            success = true,
            message = "Đã gửi lời cảm ơn đến người tặng thiệp!"
        });
    }
}