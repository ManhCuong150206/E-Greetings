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
        return template == null ? NotFound("Không tìm thấy mẫu thiệp này!") : View(template);
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

        int? validTemplateId = null;
        Template? template = null;
        if (request.TemplateId > 0)
        {
            template = await _context.Templates.FirstOrDefaultAsync(t => t.Id == request.TemplateId);
            if (template != null) validTemplateId = request.TemplateId;
        }

        // 1. Làm sạch chuỗi đường dẫn ảnh truyền vào
        string rawImagePath = CleanImagePath(request.ImagePath);

        // 2. Lưu ảnh Base64 ra file vật lý trong wwwroot/uploads/img_sentcards
        string? savedImagePath = SaveBase64Image(rawImagePath);

        // 3. Xử lý đường dẫn ảnh nếu không lưu được từ Base64
        if (string.IsNullOrEmpty(savedImagePath))
        {
            if (string.IsNullOrWhiteSpace(request.ImagePath))
            {
                // Nếu người dùng không gửi ảnh tùy chỉnh, lấy ảnh từ Template làm mặc định
                savedImagePath = CleanImagePath(template?.ImagePath);
            }
            else
            {
                // Báo lỗi rõ ràng nếu có gửi ảnh tùy chỉnh nhưng bị lỗi trong quá trình lưu file Base64
                return Json(new { success = false, message = "Lỗi trong quá trình xử lý và lưu file ảnh thiệp cá nhân hóa!" });
            }
        }

        // 4. Chuẩn hóa đường dẫn tương đối (thêm '/' ở đầu nếu thiếu)
        if (!string.IsNullOrEmpty(savedImagePath) &&
            !savedImagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            !savedImagePath.StartsWith("/", StringComparison.OrdinalIgnoreCase) &&
            !savedImagePath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            savedImagePath = "/" + savedImagePath;
        }

        // 5. Lọc bỏ chuỗi JSON nếu lỡ bị gửi nhầm vào CardHtmlContent
        string safeCardHtml = request.CardHtmlContent ?? "";
        if (safeCardHtml.TrimStart().StartsWith("{") || safeCardHtml.Contains("recipientName"))
        {
            safeCardHtml = ""; // Xóa chuỗi JSON rác trong cột HTML
        }

        var sentCard = new SentCard
        {
            AccountId = accountId,
            TemplateId = validTemplateId,
            RecipientEmail = request.RecipientEmail.Trim(),
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? "Thiệp chúc mừng" : request.Subject,
            CardHtmlContent = safeCardHtml,
            Message = request.Message, // Chuỗi JSON chứa thông tin sự kiện
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


            if (card.OpenAt == null)
            {
                card.OpenAt = DateTime.Now;
                _context.SentCards.Update(card);
                await _context.SaveChangesAsync();
            }

            // 1. Lấy đường dẫn ảnh thiệp (Ưu tiên card.ImagePath, nếu rỗng thì lấy ImagePath của Template)
            string bgImage = !string.IsNullOrEmpty(card.ImagePath)
                ? card.ImagePath
                : (card.Template?.ImagePath ?? "");

            // 2. Lấy cấu hình JSON ConfigData từ Template gốc
            string configData = card.Template?.ConfigData ?? "{}";

            ViewBag.CardId = card.Id;
            ViewBag.SenderEmail = card.SenderEmail; // Tự động lấy từ card.Account.Email
            ViewBag.RecipientEmail = card.RecipientEmail;
            ViewBag.Subject = card.Subject;
            ViewBag.ImagePath = bgImage;
            ViewBag.AudioPath = card.AudioPath ?? "";
            ViewBag.CardHtmlContent = card.CardHtmlContent; // Tự động lấy từ cột `message`
            ViewBag.OpenAt = card.OpenAt?.ToString("dd/MM/yyyy HH:mm");
            ViewBag.RecipientReply = card.RecipientReply;
            ViewBag.ConfigData = configData; // Truyền JSON ConfigData ra ViewBag
        }
        else
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

        // Kiểm tra và xử lý nếu ImagePath lưu nhầm chuỗi JSON rác
        if (!string.IsNullOrEmpty(card.ImagePath) &&
           (card.ImagePath.TrimStart().StartsWith("{") || card.ImagePath.Contains("recipientName")))
        {
            card.ImagePath = card.Template?.ImagePath;
        }

        if (string.IsNullOrEmpty(card.ImagePath) && card.Template != null)
        {
            card.ImagePath = card.Template.ImagePath;
        }

        return View(card);
    }

    // GET: /History HOẶC /Card/History
    [HttpGet]
    [Route("Card/History")]
    public async Task<IActionResult> History()
    {
        int? accountId = null;
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int parsedUserId))
        {
            accountId = parsedUserId;
        }

        // Lấy danh sách thiệp đã gửi của người dùng đang đăng nhập
        var sentCards = await _context.SentCards
            .Include(c => c.Template)
            .Where(c => c.AccountId == accountId)
            .OrderByDescending(c => c.SentAt)
            .ToListAsync();

        return View("History", sentCards);
    }

    // POST: /Card/ReplyCard
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ReplyCard(ReplyRequest request)
    {
        if (request == null || request.CardId <= 0)
        {
            return BadRequest("Dữ liệu không hợp lệ!");
        }

        var card = await _context.SentCards.FirstOrDefaultAsync(c => c.Id == request.CardId);
        if (card == null)
        {
            return NotFound("Không tìm thấy thông tin thiệp!");
        }

        // Chuẩn hóa trạng thái (tránh trường hợp truyền Yes/Maybe/No từ Javascript cũ)
        string rawStatus = request.AttendanceStatus ?? "";
        string formattedStatus = rawStatus switch
        {
            "Yes" => "Sẽ tham dự",
            "Maybe" => "Phân vân",
            "No" => "Không thể tham dự",
            _ => rawStatus
        };

        // Đóng gói Trạng thái + Lời nhắn thành JSON lưu vào RecipientReply
        var replyData = new
        {
            Status = formattedStatus,
            Text = request.ReplyMessage ?? ""
        };

        card.RecipientReply = System.Text.Json.JsonSerializer.Serialize(replyData);

        _context.SentCards.Update(card);
        await _context.SaveChangesAsync();

        // Nếu gọi qua AJAX
        if (Request.Headers["Accept"].ToString().Contains("application/json") ||
            Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, message = "Đã gửi phản hồi thành công!" });
        }

        // Submit Form thường -> Chuyển hướng lại trang xem thiệp
        return RedirectToAction("ViewCard", new { id = request.CardId });
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

        // Bỏ qua nếu dữ liệu truyền vào là chuỗi JSON rác
        if (base64String.TrimStart().StartsWith("{") || base64String.Contains("recipientName"))
        {
            return null;
        }

        // Nếu là chuỗi dữ liệu ảnh Base64
        if (base64String.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parts = base64String.Split(',');
                if (parts.Length < 2) return null;

                // XỬ LÝ SỬA LỖI BASE64: Khôi phục dấu '+' bị hóa thành ' ' trong HTTP Body
                string pureBase64 = parts[1].Trim().Replace(" ", "+");
                int mod4 = pureBase64.Length % 4;
                if (mod4 > 0)
                {
                    pureBase64 += new string('=', 4 - mod4);
                }

                byte[] imageBytes = Convert.FromBase64String(pureBase64);
                string extension = parts[0].Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

                // Đường dẫn thư mục lưu ảnh vật lý
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

        // Nếu là đường dẫn file ảnh thông thường (/uploads/... hoặc http...)
        return base64String.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
            base64String.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? base64String
            : null;
    }
}