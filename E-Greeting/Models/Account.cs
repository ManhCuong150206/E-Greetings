using System.ComponentModel.DataAnnotations.Schema;

namespace E_Greeting.Models;

public partial class Account
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Role { get; set; } = null!;

    public bool Status { get; set; }

    // BỔ SUNG TRƯỜNG NÀY: Để kiểm tra trạng thái VIP / Subscription
    [NotMapped] // 👈 Thêm thẻ này để EF Core KHÔNG tìm cột IsSubscribed trong bảng Account
    public bool IsSubscribed { get; set; } = false;

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<SentCard> SentCards { get; set; } = new List<SentCard>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}