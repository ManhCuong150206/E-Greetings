using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class Feedback
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
