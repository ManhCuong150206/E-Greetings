using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;

[Table("SentCard")]
public class SentCard
{
    [Key]
    public int Id { get; set; }

    // --- Khóa ngoại & Navigation Properties ---
    [Column("account_id")]
    public int? AccountId { get; set; }
    public virtual Account? Account { get; set; }

    [Column("template_id")]
    public int? TemplateId { get; set; }
    public virtual Template? Template { get; set; } // ✅ Đã thêm Navigation Property

    [Column("subscription_id")]
    public int? SubscriptionId { get; set; }
    public virtual Subscription? Subscription { get; set; } // ✅ Đã thêm Navigation Property

    // --- Các cột ánh xạ CSDL ---
    [Column("image_path", TypeName = "nvarchar(max)")]
    public string? ImagePath { get; set; }

    [Column("audio_path", TypeName = "nvarchar(max)")]
    public string? AudioPath { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("open_at")]
    public DateTime? OpenAt { get; set; } // ✅ Đã sửa từ Boolean sang DateTime?

    [Column("recipient_reply")]
    public string? RecipientReply { get; set; }

    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    // --- Các thuộc tính bổ sung ---
    public string SenderEmail { get; set; } = string.Empty;
    public string CardHtmlContent { get; set; } = string.Empty;
    public DateTime? CreatedDate { get; set; } = DateTime.Now;
}