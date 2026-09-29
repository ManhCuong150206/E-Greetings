namespace E_Greeting.Models;

public partial class Template
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? ImagePath { get; set; }

    public int CategoryId { get; set; }

    public string? ConfigData { get; set; }

    public bool IsFree { get; set; }

    public bool? IsActive { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
