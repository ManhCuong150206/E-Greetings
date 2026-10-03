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

    // Các trường thông tin sự kiện chi tiết
    public string? RecipientName { get; set; }
    public string? Location { get; set; }
    public string? EventTime { get; set; }

    // Đường dẫn ảnh & nhạc (hỗ trợ cả alias từ JS)
    public string? ImagePath { get; set; }
    public string? CardImageBase64 { get; set; }
    public string? AudioPath { get; set; }
    public string? AudioUrl { get; set; }

    public string? SubscriptionId { get; set; }
    public string CardHtmlContent { get; set; } = string.Empty; // Canvas HTML
    public string? Message { get; set; } // Chuỗi JSON hoặc lời nhắn bổ sung

    // Thuộc tính hẹn giờ gửi (Gói VIP)
    public DateTime? ScheduledAt { get; set; }
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

        // Kiểm tra tài khoản hiện tại có trong bảng Subscriptions không
        bool isVip = false;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            isVip = await _context.Subscriptions.AnyAsync(s => s.AccountId == userId);
        }

        ViewBag.IsVip = isVip;
        return View(template);
    }

    // POST: /Card/SaveCardConfig
    [HttpPost]
    [DisableRequestSizeLimit]
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

        // 1. Kiểm tra tài khoản VIP / Subscription
        bool isVip = false;
        if (accountId.HasValue)
        {
            isVip = await _context.Subscriptions.AnyAsync(s => s.AccountId == accountId.Value);
        }

        // 2. Xử lý Logic Hẹn giờ gửi (ScheduledAt)
        bool isScheduled = false;
        if (request.ScheduledAt.HasValue)
        {
            if (!isVip)
            {
                return Json(new
                {
                    success = false,
                    message = "Bạn cần đăng ký gói VIP/Subscription để sử dụng tính năng Hẹn giờ gửi thiệp!"
                });
            }

            // Cho phép độ trễ 1 phút so với thời gian hiện tại
            if (request.ScheduledAt.Value <= DateTime.Now.AddMinutes(-1))
            {
                return Json(new
                {
                    success = false,
                    message = $"Thời gian hẹn giờ ({request.ScheduledAt.Value:HH:mm dd/MM/yyyy}) đã trôi qua so với hiện tại ({DateTime.Now:HH:mm dd/MM/yyyy}). Vui lòng chọn thời gian ở tương lai!"
                });
            }

            isScheduled = true;
        }

        int? validTemplateId = null;
        Template? template = null;
        if (request.TemplateId > 0)
        {
            template = await _context.Templates.FirstOrDefaultAsync(t => t.Id == request.TemplateId);
            if (template != null) validTemplateId = request.TemplateId;
        }

        // 3. Chuẩn hóa nguồn ảnh & nhạc truyền vào (hỗ trợ các tên biến khác nhau từ JS)
        string inputImage = !string.IsNullOrWhiteSpace(request.ImagePath) ? request.ImagePath : (request.CardImageBase64 ?? "");
        string? inputAudio = !string.IsNullOrWhiteSpace(request.AudioPath) ? request.AudioPath : request.AudioUrl;

        string rawImagePath = CleanImagePath(inputImage);
        string? savedImagePath = SaveBase64Image(rawImagePath);

        if (string.IsNullOrEmpty(savedImagePath))
        {
            if (string.IsNullOrWhiteSpace(inputImage))
            {
                savedImagePath = CleanImagePath(template?.ImagePath);
            }
            else
            {
                return Json(new { success = false, message = "Lỗi trong quá trình xử lý và lưu file ảnh thiệp!" });
            }
        }

        if (!string.IsNullOrEmpty(savedImagePath) &&
            !savedImagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !savedImagePath.StartsWith("/", StringComparison.OrdinalIgnoreCase) &&
            !savedImagePath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            savedImagePath = "/" + savedImagePath;
        }

        // 4. Lọc bỏ chuỗi HTML không hợp lệ
        string safeCardHtml = request.CardHtmlContent ?? "";
        if (safeCardHtml.TrimStart().StartsWith("{") || safeCardHtml.Contains("recipientName"))
        {
            safeCardHtml = "";
        }

        // 5. Đóng gói dữ liệu chi tiết sự kiện vào chuỗi JSON của Message
        string finalMessage = request.Message ?? "";
        if (!string.IsNullOrWhiteSpace(request.RecipientName) || !string.IsNullOrWhiteSpace(request.Location) || !string.IsNullOrWhiteSpace(request.EventTime))
        {
            var eventDetails = new
            {
                recipientName = request.RecipientName ?? "",
                location = request.Location ?? "",
                eventTime = request.EventTime ?? "",
                note = request.Message ?? ""
            };
            finalMessage = JsonSerializer.Serialize(eventDetails);
        }

        // 6. Khởi tạo đối tượng SentCard
        var sentCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = validTemplateId,
            RecipientEmail = request.RecipientEmail.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Thiệp chúc mừng" : request.Subject.Trim(),
            CardHtmlContent = safeCardHtml,
            Message = finalMessage,
            ImagePath = savedImagePath,
            AudioPath = inputAudio,

            ScheduledAt = isScheduled ? request.ScheduledAt : null,
            SentAt = isScheduled ? null : DateTime.Now
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

        // 7. Gửi Email ngay nếu KHÔNG phải bài hẹn giờ
        if (!isScheduled)
        {
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
        }

        return Json(new
        {
            success = true,
            message = isScheduled
                ? $"Thiệp đã được lưu thành công và hẹn giờ gửi tự động vào lúc {request.ScheduledAt!.Value:HH:mm dd/MM/yyyy}!"
                : "Lưu thiệp & Gửi Email thành công!",
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
    public async Task<IActionResult> ReplyCard(ReplyRequest request)
    {
        // Nếu gửi dạng JSON Body thô, tự động parse nếu request rỗng
        if (request == null || request.CardId <= 0)
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    request = System.Text.Json.JsonSerializer.Deserialize<ReplyRequest>(body, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new ReplyRequest();
                }
            }
            catch { }
        }

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

        card.RecipientReply = System.Text.Json.JsonSerializer.Serialize(replyData);
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