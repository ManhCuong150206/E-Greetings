<<<<<<< HEAD
﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;
=======
﻿namespace E_Greeting.Models;
>>>>>>> dec44cdd333b59f91d6119fcc2bec2058dc871be

[Table("Template")]
public partial class Template
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("image_path")]
    public string? ImagePath { get; set; }

    [Column("category_id")]
    public int CategoryId { get; set; }

<<<<<<< HEAD
    [Column("config_data")]
=======
>>>>>>> dec44cdd333b59f91d6119fcc2bec2058dc871be
    public string? ConfigData { get; set; }

    [Column("is_free")]
    public bool IsFree { get; set; }

<<<<<<< HEAD
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
=======
    public bool? IsActive { get; set; }
>>>>>>> dec44cdd333b59f91d6119fcc2bec2058dc871be

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}