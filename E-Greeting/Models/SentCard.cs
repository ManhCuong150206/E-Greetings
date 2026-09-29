using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;

[Table("SentCard")] // Định danh chính xác tên bảng trong CSDL
public class SentCard
{
    [Key]
    public int Id { get; set; }

    // --- Các khóa ngoại ---
    public int? AccountId { get; set; }
    public virtual Account? Account { get; set; }

    public int? TemplateId { get; set; }
    public virtual Template? Template { get; set; }

    public int? SubscriptionId { get; set; }
    public virtual Subscription? Subscription { get; set; }

    // --- Ánh xạ chính xác vào các cột Snake_case trong CSDL ---
    [Column("image_path", TypeName = "nvarchar(max)")]
    public string? ImagePath { get; set; }

    [Column("audio_path", TypeName = "nvarchar(max)")]
    public string? AudioPath { get; set; }

    public string? Message { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("open_at")]
    public DateTime? OpenAt { get; set; }

    [Column("recipient_reply")]
    public string? RecipientReply { get; set; }

    // --- Các thuộc tính bổ sung ---
    public string SenderEmail { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string CardHtmlContent { get; set; } = string.Empty;

    //public string? AudioUrl { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}