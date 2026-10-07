using System.Net;
using System.Net.Mail;

namespace E_Greeting.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config) => _config = config;

    public async Task SendCardEmailAsync(string toEmail, string subject, string cardViewUrl, string senderName)
    {
        var smtpServer = _config["EmailSettings:SmtpServer"];
        var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
        var senderEmail = _config["EmailSettings:SenderEmail"];
        var displayName = _config["EmailSettings:SenderName"] ?? "E-Greeting";
        var password = _config["EmailSettings:Password"];

        var message = new MailMessage();
        message.From = new MailAddress(senderEmail!, displayName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = string.IsNullOrWhiteSpace(subject) ? "💌 You've received a greeting card!" : subject;

        // English HTML Email Template
        message.Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 25px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
                    <div style='text-align: center; padding-bottom: 15px; border-bottom: 2px solid #f1f5f9;'>
                        <h1 style='color: #e11d48; margin: 0; font-size: 24px;'>💖 E-Greeting System</h1>
                    </div>
                    
                    <div style='padding: 20px 0;'>
                        <p style='font-size: 16px; color: #334155;'>Hello,</p>
                        <p style='font-size: 16px; color: #334155; line-height: 1.6;'>
                            <strong>{senderName}</strong> has created and sent you a special greeting card complete with background music and custom effects!
                        </p>
                        
                        <div style='text-align: center; margin: 35px 0;'>
                            <a href='{cardViewUrl}' target='_blank' style='background-color: #e11d48; color: #ffffff; padding: 14px 30px; text-decoration: none; border-radius: 30px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 12px rgba(225, 29, 72, 0.3); display: inline-block;'>
                                💌 OPEN YOUR CARD NOW
                            </a>
                        </div>

                        <p style='font-size: 13px; color: #64748b; text-align: center;'>
                            If the button above does not work, copy and paste the link below into your browser:<br>
                            <a href='{cardViewUrl}' style='color: #2563eb;'>{cardViewUrl}</a>
                        </p>
                    </div>

                    <div style='border-top: 1px solid #f1f5f9; padding-top: 15px; text-align: center; font-size: 12px; color: #94a3b8;'>
                        <p style='margin: 0;'>This is an automated email from the E-Greeting System. Please do not reply directly to this email.</p>
                    </div>
                </div>";
        message.IsBodyHtml = true;

        using var client = new SmtpClient(smtpServer, port)
        {
            Credentials = new NetworkCredential(senderEmail, password),
            EnableSsl = true
        };

        await client.SendMailAsync(message);
    }
}