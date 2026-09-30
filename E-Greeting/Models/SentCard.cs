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

    [ForeignKey("AccountId")]
    public virtual Account? Account { get; set; }

    [Column("template_id")]
    public int? TemplateId { get; set; }

    [ForeignKey("TemplateId")]
    public virtual Template? Template { get; set; }

    [Column("subscription_id")]
    public int? SubscriptionId { get; set; }

    [ForeignKey("SubscriptionId")]
    public virtual Subscription? Subscription { get; set; }

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
    public DateTime? OpenAt { get; set; }

    [Column("recipient_reply")]
    public string? RecipientReply { get; set; }

    [Column("recipient_email")]
    public string RecipientEmail { get; set; } = string.Empty;

    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    // --- Các thuộc tính bổ sung (Dùng [NotMapped] để EF Core KHÔNG tìm trong CSDL) ---

    // 1. Lưu/Đọc nội dung HTML thiệp trực tiếp qua cột `message`
    [NotMapped]
    public string CardHtmlContent
    {
        get => Message ?? string.Empty;
        set => Message = value;
    }

    // 2. Đọc/Ghi ngày tạo thiệp trực tiếp qua cột `sent_at`
    [NotMapped]
    public DateTime? CreatedDate
    {
        get => SentAt;
        set => SentAt = value;
    }

    // 3. Tự động lấy Email người gửi từ bảng Account liên kết
    [NotMapped]
    public string SenderEmail => Account?.Email ?? string.Empty; // Giả định thuộc tính Email bên class Account tên là Email
}