using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;

[Table("Template")]
public partial class Template
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("image_path")]
    public string ImagePath { get; set; } = null!;

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("config_data")]
    public string? ConfigData { get; set; } 

    [Column("is_free")]
    public bool IsFree { get; set; } = true;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}