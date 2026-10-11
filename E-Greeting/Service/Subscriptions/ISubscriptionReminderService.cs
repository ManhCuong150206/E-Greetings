using System.Threading;
using System.Threading.Tasks;

namespace E_Greeting.Service.Subscriptions
{
    public interface ISubscriptionReminderService
    {
        // Trigger function to send task reminder emails or call from a background task.
        Task CheckAndSendExpiryRemindersAsync(CancellationToken stoppingToken = default);
    }
}