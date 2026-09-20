using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class Subscription
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public string NameSub { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual ICollection<SubscriptionRecipient> SubscriptionRecipients { get; set; } = new List<SubscriptionRecipient>();
}
