namespace E_Greeting.Models;

public class UserHistoryViewModel
{
    public List<SentCard> SentCards { get; set; } = new();

    public List<Subscription> Subscriptions { get; set; } = new();
}