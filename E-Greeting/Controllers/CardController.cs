using E_Greeting.Models;
using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers;

// DTO chứa dữ liệu cấu hình thiệp từ FE gửi lên
public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? FontStyle { get; set; }
    public string? TextColor { get; set; }
}

public class CardController : Controller
{
    private readonly AppDbContext _context;

    public CardController(AppDbContext context) => _context = context;

    // GET: /Card/Personalize/1
    [HttpGet]
    public IActionResult Personalize(int id)
    {
        // Tạo mẫu thiệp giả tạm thời để test giao diện Frontend không lo bị treo SQL
        var template = new Template
        {
            Id = id > 0 ? id : 1,
            Name = "Thiệp Chúc Mừng Mẫu",
            ImagePath = "https://picsum.photos/800/600?random=" + id
        };

        return View(template);
    }
    //[HttpPost]
    //public async Task<IActionResult> SaveCardConfig([FromBody] SaveCardRequest request)
    //{
    //    if (request == null || string.IsNullOrWhiteSpace(request.RecipientEmail))
    //    {
    //        return Json(new { success = false, message = "Vui lòng nhập Email người nhận hợp lệ!" });
    //    }

    //    try
    //    {
    //        // Lấy AccountId nếu người dùng đã đăng nhập
    //        int accountId = 1; // Mặc định ID test nếu chưa bật cookie
    //        if (User.Identity != null && User.Identity.IsAuthenticated)
    //        {
    //            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
    //            if (int.TryParse(userIdClaim, out int parsedId)) accountId = parsedId;
    //        }

    //        var newSentCard = new SentCard
    //        {
    //            TemplateId = request.TemplateId > 0 ? request.TemplateId : 1,
    //            AccountId = accountId,
    //            RecipientEmail = request.RecipientEmail,
    //            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Bạn nhận được một thiệp chúc mừng!" : request.Subject,
    //            Message = request.Message,
    //            AudioPath = request.AudioUrl,
    //            SentAt = DateTime.Now
    //        };

    //        _context.SentCards.Add(newSentCard);
    //        await _context.SaveChangesAsync();

    //        return Json(new { success = true, message = "Lưu thiệp thành công!", cardId = newSentCard.Id });
    //    }
    //    catch (Exception ex)
    //    {
    //        return Json(new { success = false, message = $"Lỗi lưu DB: {ex.Message}" });
    //    }
    //}
    [HttpPost]
    public IActionResult SaveCardConfig([FromBody] SaveCardRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return Json(new { success = false, message = "Vui lòng nhập Email người nhận hợp lệ!" });
        }

        // Trả về kết quả thành công ngay lập tức
        return Json(new
        {
            success = true,
            message = "Lưu & gửi thiệp thành công!",
            cardId = 101
        });
    }
}