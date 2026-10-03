using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Services;

public class EmailSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSchedulerService> _logger;

    public EmailSchedulerService(IServiceScopeFactory scopeFactory, ILogger<EmailSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== Email Scheduler Service (Timer Hẹn Giờ) Đã Bắt Đầu ===");

        // Đặt thời gian Timer quét CSDL: 1 phút / lần (60 giây)
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckAndSendScheduledCardsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi quét thiệp hẹn giờ!");
            }
        }
    }

    private async Task CheckAndSendScheduledCardsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetService<IEmailService>();

        var now = DateTime.Now;

        // Lấy danh sách thiệp: CHƯA GỬI (SentAt == null) VÀ ĐÃ TỚI GIỜ HẸN (ScheduledAt <= now)
        var pendingCards = await dbContext.SentCards
            .Where(c => c.SentAt == null && c.ScheduledAt != null && c.ScheduledAt <= now)
            .ToListAsync();

        if (!pendingCards.Any()) return;

        foreach (var card in pendingCards)
        {
            try
            {
                // 1. Thực hiện gửi Mail cho người nhận
                if (emailService != null && !string.IsNullOrEmpty(card.RecipientEmail))
                {
                    // ĐỔI TẠI ĐÂY: Dùng link localhost cố định (thay port 7123 bằng port ứng dụng bạn đang chạy)
                    string cardViewUrl = $"https://localhost:7123/Card/ViewCard/{card.Id}";
                    string senderName = "Hệ thống E-Greeting (Hẹn giờ)";

                    await emailService.SendCardEmailAsync(card.RecipientEmail, card.Subject, cardViewUrl, senderName);
                }

                // 2. Gửi xong thì cập nhật SentAt = Ngày giờ hiện tại
                card.SentAt = DateTime.Now;
                dbContext.SentCards.Update(card);

                _logger.LogInformation($"[HẸN GIỜ SUCCESS] Đã tự động gửi thiệp ID {card.Id} cho {card.RecipientEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[HẸN GIỜ ERROR] Gửi thất bại thiệp ID {card.Id}");
            }
        }

        // Lưu thay đổi vào CSDL
        await dbContext.SaveChangesAsync();
    }
}