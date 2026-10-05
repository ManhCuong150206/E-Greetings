using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;

public partial class Subscription
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; }

    public int? ServiceId { get; set; }

    // Thuộc tính mới: Đánh dấu trạng thái đã gửi email nhắc nhở hết hạn VIP hay chưa
    [Column("is_reminder_sent")]
    public bool IsReminderSent { get; set; } = false;

    public virtual Account Account { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual Service? Service { get; set; }

    public virtual ICollection<SubscriptionRecipient> SubscriptionRecipients { get; set; } = new List<SubscriptionRecipient>();
}