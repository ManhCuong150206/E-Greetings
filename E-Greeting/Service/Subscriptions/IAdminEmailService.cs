using System.Threading.Tasks;

namespace E_Greeting.Service.Subscriptions
{
    public interface IAdminEmailService
    {
        Task SendAdminNotificationEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}