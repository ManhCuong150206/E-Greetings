using E_Greeting.Models;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace E_Greeting.Controllers;

// --- DTO Requests ---
public class SaveCardRequest
{
    public int TemplateId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? Location { get; set; }
    public string? EventTime { get; set; }
    public string? ImagePath { get; set; }
    public string? CardImageBase64 { get; set; }
    public string? AudioPath { get; set; }
    public string? AudioUrl { get; set; }
    public string? SubscriptionId { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty;
    public string? Message { get; set; }

    // Hỗ trợ cả DateTime lẫn chuỗi string từ Frontend
    public DateTime? ScheduledAt { get; set; }
    public string? ScheduledAtStr { get; set; }
}
public class ReplyRequest
{
    public int CardId { get; set; }
    public string ReplyMessage { get; set; } = string.Empty;
    public string? AttendanceStatus { get; set; }
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
        if (template == null) return NotFound("Không tìm thấy mẫu thiệp này!");

        bool isVip = false;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            isVip = await _context.Subscriptions.AnyAsync(s => s.AccountId == userId && s.IsActive);
        }

        ViewBag.IsVip = isVip;
        return View(template);
    }

    // POST: /Card/SaveCardConfig
    [HttpPost]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> SaveCardConfig([FromBody] SaveCardRequest req)
    {
        if (req == null)
            return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });

        // 1. Lấy AccountId từ User đang đăng nhập
        int? accountId = null;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int parsedUserId))
        {
            accountId = parsedUserId;
        }

        // 2. Xử lý lưu ảnh Base64
        string? finalImagePath = SaveBase64Image(req.CardImageBase64)
                               ?? SaveBase64Image(req.ImagePath)
                               ?? req.ImagePath;

        // 3. Xử lý ép kiểu Ngày/Giờ hẹn gửi từ Frontend
        DateTime? scheduledTime = req.ScheduledAt;
        if (!scheduledTime.HasValue && !string.IsNullOrWhiteSpace(req.ScheduledAtStr))
        {
            if (DateTime.TryParse(req.ScheduledAtStr, out var parsedDate))
            {
                scheduledTime = parsedDate;
            }
        }

        // 4. Khởi tạo Entity SentCard
        var newCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = req.TemplateId,
            RecipientEmail = req.RecipientEmail,
            Subject = req.Subject,
            AudioPath = req.AudioPath ?? req.AudioUrl,
            ImagePath = finalImagePath,
            CardHtmlContent = req.CardHtmlContent,
            Message = !string.IsNullOrWhiteSpace(req.Message)
                ? req.Message
                : JsonSerializer.Serialize(new
                {
                    recipientName = req.RecipientName,
                    eventAddress = req.Location,
                    eventTime = req.EventTime
                })
        };

        // 5. LOGIC HẸN GIỜ: Kiểm tra thời gian hẹn so với hiện tại
        if (scheduledTime.HasValue)
        {
            newCard.ScheduledAt = scheduledTime.Value; // Lưu thời gian hẹn vào DB

            if (scheduledTime.Value > DateTime.Now)
            {
                // Hẹn giờ tương lai -> Chưa gửi email ngay lúc này
                newCard.SentAt = null;
            }
            else
            {
                // Giờ hẹn đã qua hoặc bằng hiện tại -> Cho gửi ngay
                newCard.SentAt = DateTime.Now;
            }
        }
        else
        {
            // Không chọn hẹn giờ -> Gửi ngay
            newCard.ScheduledAt = null;
            newCard.SentAt = DateTime.Now;
        }

        // 6. Lưu vào Database
        _context.SentCards.Add(newCard);
        await _context.SaveChangesAsync();

        // 7. CHỈ GỬI EMAIL NẾU SentAt CÓ GIÁ TRỊ (Không hẹn giờ hoặc đã tới giờ)
        if (newCard.SentAt.HasValue)
        {
            string cardViewUrl = $"{Request.Scheme}://{Request.Host}/Card/ViewCard/{newCard.Id}";
            string senderName = User.Identity?.Name ?? "Hệ thống E-Greeting";

            try
            {
                await _emailService.SendCardEmailAsync(newCard.RecipientEmail, newCard.Subject, cardViewUrl, senderName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Error]: {ex.Message}");
            }
        }

        // 8. Trả về thông báo tương ứng cho Client
        return Json(new
        {
            success = true,
            message = (newCard.ScheduledAt.HasValue && !newCard.SentAt.HasValue)
                ? $"Đã lên lịch hẹn giờ gửi thiệp vào lúc {newCard.ScheduledAt.Value:HH:mm dd/MM/yyyy}!"
                : "Đã gửi thiệp thành công!",
            cardId = newCard.Id
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
            ViewBag.CardId = id;
            ViewBag.SenderEmail = "nguoidung@example.com";
            ViewBag.ImagePath = "";
            ViewBag.AudioPath = "";
            ViewBag.CardHtmlContent = "<div class='p-4 text-center text-muted'>Thiệp không tồn tại hoặc đã bị xóa.</div>";
            ViewBag.OpenAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.ConfigData = "{}";

            return NotFound("Thiệp không tồn tại hoặc đã bị xóa.");
        }

        if (card.OpenAt == null)
        {
            card.OpenAt = DateTime.Now;
            _context.SentCards.Update(card);
            await _context.SaveChangesAsync();
        }

        if (!string.IsNullOrEmpty(card.ImagePath) &&
           (card.ImagePath.TrimStart().StartsWith("{") || card.ImagePath.Contains("recipientName")))
        {
            card.ImagePath = card.Template?.ImagePath;
        }

        if (string.IsNullOrEmpty(card.ImagePath) && card.Template != null)
        {
            card.ImagePath = card.Template.ImagePath;
        }

        string bgImage = !string.IsNullOrEmpty(card.ImagePath)
            ? card.ImagePath
            : (card.Template?.ImagePath ?? "");

        string configData = card.Template?.ConfigData ?? "{}";

        ViewBag.CardId = card.Id;
        ViewBag.SenderEmail = card.SenderEmail ?? card.Account?.Email;
        ViewBag.RecipientEmail = card.RecipientEmail;
        ViewBag.Subject = card.Subject;
        ViewBag.ImagePath = bgImage;
        ViewBag.AudioPath = card.AudioPath ?? "";
        ViewBag.CardHtmlContent = card.CardHtmlContent;
        ViewBag.OpenAt = card.OpenAt?.ToString("dd/MM/yyyy HH:mm");
        ViewBag.RecipientReply = card.RecipientReply;
        ViewBag.ConfigData = configData;

        return View(card);
    }

    // GET: /History HOẶC /Card/History
    [HttpGet]
    [Route("History")]
    public async Task<IActionResult> History(string tab = "cards")
    {
        int? accountId = null;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int parsedUserId))
        {
            accountId = parsedUserId;
        }

        var sentCards = await _context.SentCards
            .Include(c => c.Template)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.SentAt ?? c.ScheduledAt)
            .ToListAsync();

        var userSubscriptions = await _context.Subscriptions
             .Include(s => s.Service)
             .Include(s => s.Payments)
             .Where(s => s.AccountId == accountId)
             .OrderByDescending(s => s.Id)
             .ToListAsync();

        ViewBag.ActiveTab = tab;

        var viewModel = new UserHistoryViewModel
        {
            SentCards = sentCards,
            Subscriptions = userSubscriptions
        };

        return View("History", viewModel);
    }

    // POST: /Card/ReplyCard
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ReplyCard([FromBody] ReplyRequest request)
    {
        if (request == null || request.CardId <= 0)
        {
            return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ!" });
        }

        var card = await _context.SentCards.FirstOrDefaultAsync(c => c.Id == request.CardId);
        if (card == null)
        {
            return Json(new { success = false, message = "Không tìm thấy thông tin thiệp!" });
        }

        string rawStatus = request.AttendanceStatus ?? "";
        string formattedStatus = rawStatus switch
        {
            "Yes" => "Sẽ tham dự",
            "Maybe" => "Phân vân",
            "No" => "Không thể tham dự",
            _ => rawStatus
        };

        var replyData = new
        {
            Status = formattedStatus,
            Text = request.ReplyMessage ?? ""
        };

        card.RecipientReply = JsonSerializer.Serialize(replyData);
        _context.SentCards.Update(card);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Đã gửi phản hồi thành công!" });
    }

    // --- HÀM BỔ TRỢ XỬ LÝ ẢNH ---
    private string CleanImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        path = path.Trim();
        if (path.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            path = path[4..];
            if (path.EndsWith(")")) path = path[..^1];
            path = path.Trim('\'', '"', ' ');
        }
        return path.Trim('\'', '"', ' ');
    }

    private string? SaveBase64Image(string? base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String)) return null;

        base64String = CleanImagePath(base64String);

        if (base64String.TrimStart().StartsWith("{") || base64String.Contains("recipientName"))
        {
            return null;
        }

        if (base64String.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parts = base64String.Split(',');
                if (parts.Length < 2) return null;

                string pureBase64 = parts[1].Trim().Replace(" ", "+");
                int mod4 = pureBase64.Length % 4;
                if (mod4 > 0)
                {
                    pureBase64 += new string('=', 4 - mod4);
                }

                byte[] imageBytes = Convert.FromBase64String(pureBase64);
                string extension = parts[0].Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

                string webRoot = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                string folderPath = Path.Combine(webRoot, "uploads", "img_sentcards");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"card_{Guid.NewGuid()}{extension}";
                string filePath = Path.Combine(folderPath, fileName);
                System.IO.File.WriteAllBytes(filePath, imageBytes);

                return $"/uploads/img_sentcards/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi lưu file Base64: " + ex.Message);
                return null;
            }
        }

        return base64String.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
               base64String.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? base64String
            : null;
    }
}