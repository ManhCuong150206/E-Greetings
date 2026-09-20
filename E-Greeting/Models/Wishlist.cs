using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class Wishlist
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int TemplateId { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual Template Template { get; set; } = null!;
}
