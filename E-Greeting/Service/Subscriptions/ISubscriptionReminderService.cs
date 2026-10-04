using System.Threading;
using System.Threading.Tasks;

namespace E_Greeting.Service.Subscriptions
{
    public interface ISubscriptionReminderService
    {
        // Hàm kích hoạt gửi email nhắc nhở thủ công hoặc gọi từ Background Task
        Task CheckAndSendExpiryRemindersAsync(CancellationToken stoppingToken = default);
    }
}