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
        message.Subject = string.IsNullOrWhiteSpace(subject) ? "💌 Bạn nhận được một tấm thiệp chúc mừng!" : subject;

        // Giao diện HTML của Email gửi đến người nhận
        message.Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 25px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
                    <div style='text-align: center; padding-bottom: 15px; border-bottom: 2px solid #f1f5f9;'>
                        <h1 style='color: #e11d48; margin: 0; font-size: 24px;'>💖 E-Greeting System</h1>
                    </div>
                    
                    <div style='padding: 20px 0;'>
                        <p style='font-size: 16px; color: #334155;'>Xin chào,</p>
                        <p style='font-size: 16px; color: #334155; line-height: 1.6;'>
                            <strong>{senderName}</strong> vừa tạo và gửi tặng bạn một tấm thiệp chúc mừng đặc biệt kèm nhạc nền và hiệu ứng đẹp mắt!
                        </p>
                        
                        <div style='text-align: center; margin: 35px 0;'>
                            <a href='{cardViewUrl}' target='_blank' style='background-color: #e11d48; color: #ffffff; padding: 14px 30px; text-decoration: none; border-radius: 30px; font-weight: bold; font-size: 16px; box-shadow: 0 4px 12px rgba(225, 29, 72, 0.3); display: inline-block;'>
                                💌 MỞ XEM THIỆP NGAY
                            </a>
                        </div>

                        <p style='font-size: 13px; color: #64748b; text-align: center;'>
                            Nếu nút trên không hoạt động, bạn có thể copy link sau dán vào trình duyệt:<br>
                            <a href='{cardViewUrl}' style='color: #2563eb;'>{cardViewUrl}</a>
                        </p>
                    </div>

                    <div style='border-top: 1px solid #f1f5f9; padding-top: 15px; text-align: center; font-size: 12px; color: #94a3b8;'>
                        <p style='margin: 0;'>Đây là email tự động từ hệ thống E-Greeting. Vui lòng không trả lời trực tiếp email này.</p>
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