using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace E_Greeting.Service.Subscriptions
{
    public class SubscriptionReminderService : BackgroundService, ISubscriptionReminderService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SubscriptionReminderService> _logger;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();

        public SubscriptionReminderService(
            IServiceProvider serviceProvider,
            ILogger<SubscriptionReminderService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        // --- HÀM TỰ ĐỘNG BẮT DOMAIN DANG RUN TRONG TERMINAL NGROK ---
        private async Task<string> GetActiveNgrokDomainAsync()
        {
            try
            {
                // Ngrok mặc định mở API quản lý tại http://127.0.0.1:4040/api/tunnels
                var response = await _httpClient.GetStringAsync("http://127.0.0.1:4040/api/tunnels");
                using var doc = JsonDocument.Parse(response);
                var tunnels = doc.RootElement.GetProperty("tunnels");

                foreach (var tunnel in tunnels.EnumerateArray())
                {
                    string publicUrl = tunnel.GetProperty("public_url").GetString() ?? "";
                    if (publicUrl.StartsWith("https://"))
                    {
                        _logger.LogInformation($"[VIP Reminder] Tự động bắt được Ngrok Domain đang chạy trong Terminal: {publicUrl}");
                        return publicUrl;
                    }
                }
            }
            catch
            {
                _logger.LogWarning("[VIP Reminder] Không kết nối được API Ngrok local (hoặc ngrok chưa bật). Dùng Domain từ appsettings.json.");
            }

            // Nếu ngrok chưa chạy hoặc lỗi, lấy Domain mặc định trong appsettings.json
            string fallbackDomain = _configuration["Domain"]
                                 ?? _configuration["AppSettings:Domain"]
                                 ?? "https://localhost:7130";
            return fallbackDomain;
        }

        public async Task CheckAndSendExpiryRemindersAsync(CancellationToken stoppingToken = default)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var adminEmailService = scope.ServiceProvider.GetService<IAdminEmailService>();

                if (adminEmailService != null)
                {
                    var today = DateOnly.FromDateTime(DateTime.Now);
                    var targetDate = today.AddDays(3);

                    var expiringSubs = await context.Subscriptions
                        .Include(s => s.Account)
                        .Include(s => s.Service)
                        .Where(s => s.IsActive && s.EndDate == targetDate && !s.IsReminderSent)
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation($"[VIP Reminder] Tìm thấy {expiringSubs.Count} gói VIP hết hạn vào ngày {targetDate:dd/MM/yyyy} chưa gửi mail.");

                    // 1. TỰ ĐỘNG LẤY DOMAIN NGROK ĐANG START Ở TERMINAL (Hoặc giữ nguyên appsettings nếu không bật)
                    string domain = await GetActiveNgrokDomainAsync();
                    domain = domain.TrimEnd('/');

                    string subscriptionUrl = $"{domain}/Subscription";

                    foreach (var sub in expiringSubs)
                    {
                        if (sub.Account != null && !string.IsNullOrEmpty(sub.Account.Email))
                        {
                            string serviceName = sub.Service?.Name ?? "VIP";
                            string subject = "[E-Greeting] Thông Báo: Gói VIP của bạn sắp hết hạn!";

                            string refCode = DateTime.Now.ToString("yyyyMMddHHmmss");

                            string body = $@"
                                <div style='font-family: Arial, sans-serif; padding: 25px; border: 1px solid #e0e0e0; border-radius: 12px; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                                    <h3 style='color: #0d6efd; margin-top: 0;'>Xin chào {sub.Account.Name},</h3>
                                    <p style='font-size: 15px; color: #333; line-height: 1.5;'>Gói dịch vụ <b>{serviceName}</b> của bạn tại hệ thống <b>E-Greeting</b> sẽ hết hạn vào ngày <b>{sub.EndDate:dd/MM/yyyy}</b> (còn 3 ngày nữa).</p>
                                    <p style='font-size: 15px; color: #333; line-height: 1.5;'>Để không bị gián đoạn trải nghiệm tạo thiệp độc đáo, bạn hãy chủ động gia hạn gói cước nhé!</p>
                                    
                                    <div style='margin: 25px 0;'>
                                        <a href='{subscriptionUrl}' style='padding: 12px 28px; background-color: #ffc107; color: #000000; text-decoration: none; border-radius: 25px; font-weight: bold; font-size: 15px; display: inline-block;'>Gia Hạn Ngay</a>
                                    </div>
                                    
                                    <p style='font-size: 12px; color: #6c757d;'>Hoặc truy cập nhanh qua đường dẫn: <a href='{subscriptionUrl}' style='color: #0d6efd;'>{subscriptionUrl}</a></p>

                                    <hr style='margin-top: 25px; border: none; border-top: 1px solid #eeeeee;' />
                                    <p style='font-size: 12px; color: #777777; margin-bottom: 5px;'>Trân trọng,<br/>Đội ngũ E-Greeting</p>
                                    <span style='font-size: 10px; color: #ffffff; display: none;'>Ref: {refCode}</span>
                                </div>";

                            try
                            {
                                await adminEmailService.SendAdminNotificationEmailAsync(sub.Account.Email, subject, body);

                                sub.IsReminderSent = true;
                                _logger.LogInformation($"[VIP Reminder] Đã gửi mail nhắc gói {serviceName} thành công cho User: {sub.Account.Email}");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"[VIP Reminder] Lỗi gửi mail tới {sub.Account.Email}");
                            }
                        }
                    }

                    if (expiringSubs.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSendExpiryRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi chạy Background Service gửi mail nhắc hết hạn VIP.");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}