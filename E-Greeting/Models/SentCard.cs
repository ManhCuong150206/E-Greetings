namespace E_Greeting.Models;

public class SentCard
{
    public int Id { get; set; }

    // --- Các thuộc tính & Khóa ngoại cũ (Giữ nguyên để AppDbContext không bị lỗi) ---
    public int? AccountId { get; set; }
    public virtual Account? Account { get; set; }

    public int? TemplateId { get; set; }
    public virtual Template? Template { get; set; }

    public int? SubscriptionId { get; set; }
    public virtual Subscription? Subscription { get; set; }

    public string? ImagePath { get; set; }
    public string? AudioPath { get; set; }
    public string? Message { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }


    // --- Các thuộc tính mới bổ sung cho Người 3 ---
    public string SenderEmail { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string CardHtmlContent { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? OpenAt { get; set; }
    public string? RecipientReply { get; set; }
}