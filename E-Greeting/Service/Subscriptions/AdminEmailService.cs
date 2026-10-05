using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace E_Greeting.Service.Subscriptions
{
    public class AdminEmailService : IAdminEmailService
    {
        private readonly IConfiguration _config;

        public AdminEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAdminNotificationEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var smtpServer = _config["EmailSettings:SmtpServer"];
            var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
            var senderEmail = _config["EmailSettings:SenderEmail"];
            var displayName = _config["EmailSettings:SenderName"] ?? "E-Greeting Admin System";
            var password = _config["EmailSettings:Password"];

            var message = new MailMessage
            {
                From = new MailAddress(senderEmail!, displayName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }
    }
}