using System;
using System.Collections.Generic;

namespace E_Greeting.Models;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Template> Templates { get; set; } = new List<Template>();
}
