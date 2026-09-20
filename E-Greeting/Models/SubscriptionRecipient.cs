using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class SubscriptionRecipient
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    public string Email { get; set; } = null!;

    public virtual Subscription Subscription { get; set; } = null!;
}
