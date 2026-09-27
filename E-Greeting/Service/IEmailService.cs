namespace E_Greeting.Services;

public interface IEmailService
{
    Task SendCardEmailAsync(string toEmail, string subject, string cardViewUrl, string senderName);
}