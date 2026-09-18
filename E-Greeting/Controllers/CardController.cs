using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers;

// Class DTO định kiểu rõ ràng cho Thiệp
public class CardModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

// Class DTO nhận dữ liệu gửi thiệp
public class SendCardRequest
{
    public int CardId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AudioUrl { get; set; } // Đường dẫn file MP3
}

public class CardController : Controller
{
    // GET: /Card/Personalize/1
    public IActionResult Personalize(int id)
    {
        // Khởi tạo danh sách thiệp mẫu
        List<CardModel> cards = new()
        {
            new CardModel { Id = 1, Name = "Sinh Nhật Ngọt Ngào", ImagePath = "https://picsum.photos/800/600?random=1" },
            new CardModel { Id = 2, Name = "Thiệp Cưới Sang Trọng", ImagePath = "https://picsum.photos/800/600?random=2" },
            new CardModel { Id = 3, Name = "Thiệp Mời Tiệc Tân Gia", ImagePath = "https://picsum.photos/800/600?random=3" },
            new CardModel { Id = 4, Name = "Chúc Mừng Năm Mới 2026", ImagePath = "https://picsum.photos/800/600?random=4" },
            new CardModel { Id = 5, Name = "Sinh Nhật Tuổi Hồng", ImagePath = "https://picsum.photos/800/600?random=5" },
            new CardModel { Id = 6, Name = "Kỷ Niệm Ngày Cưới", ImagePath = "https://picsum.photos/800/600?random=6" },
            new CardModel { Id = 7, Name = "Lễ Hội Mùa Xuân", ImagePath = "https://picsum.photos/800/600?random=7" },
            new CardModel { Id = 8, Name = "Thiệp Tri Ân Đã Ghé Thăm", ImagePath = "https://picsum.photos/800/600?random=8" }
        };

        // Lấy thiệp theo id, nếu không tìm thấy sẽ lấy thiệp đầu tiên
        CardModel selectedCard = cards.FirstOrDefault(c => c.Id == id) ?? cards[0];

        ViewBag.CardId = selectedCard.Id;
        ViewBag.CardName = selectedCard.Name;
        ViewBag.ImagePath = selectedCard.ImagePath;

        return View();
    }

    // POST: /Card/SendCard
    [HttpPost]
    public IActionResult SendCard([FromBody] SendCardRequest model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.RecipientEmail))
        {
            return Json(new { success = false, message = "Vui lòng nhập email người nhận hợp lệ!" });
        }

        return Json(new { success = true, message = $"Thiệp đã được gửi thành công đến {model.RecipientEmail}!" });
    }
}