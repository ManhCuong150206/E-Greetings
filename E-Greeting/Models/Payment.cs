using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class Payment
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public string Status { get; set; } = null!;

    public string? PaymentMethod { get; set; }

    public virtual Subscription Subscription { get; set; } = null!;
}
