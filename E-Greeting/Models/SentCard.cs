using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class SentCard
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int TemplateId { get; set; }

    public int? SubscriptionId { get; set; }

    public string RecipientEmail { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string? Message { get; set; }

    public string? ImagePath { get; set; }

    public string? AudioPath { get; set; }

    public DateTime SentAt { get; set; }

    public DateTime? OpenAt { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public string? RecipientReply { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Subscription? Subscription { get; set; }

    public virtual Template Template { get; set; } = null!;
}
