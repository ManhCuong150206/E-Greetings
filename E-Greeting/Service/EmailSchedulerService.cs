using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Services;

public class EmailSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailSchedulerService> _logger;

    public EmailSchedulerService(IServiceProvider serviceProvider, ILogger<EmailSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var now = DateTime.Now;

                    // Tìm các thiệp ĐANG CHỜ (SentAt == null) và ĐÃ TỚI HẠN GỬI (ScheduledAt <= now)
                    var pendingCards = await context.SentCards
                        .Where(c => c.SentAt == null && c.ScheduledAt != null && c.ScheduledAt <= now)
                        .ToListAsync(stoppingToken);

                    foreach (var card in pendingCards)
                    {
                        try
                        {
                            string fullCardUrl = $"https://localhost:7130/Card/ViewCard/{card.Id}";

                            await emailService.SendCardEmailAsync(
                                card.RecipientEmail,
                                card.Subject,
                                fullCardUrl,
                                "hethong@e-greeting.com"
                            );

                            // Đánh dấu đã gửi thành công
                            card.SentAt = DateTime.Now;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Lỗi gửi email thiệp ID {card.Id}: {ex.Message}");
                        }
                    }

                    if (pendingCards.Any())
                    {
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi quét email hẹn giờ");
            }

            // Quét lại CSDL sau mỗi 1 phút
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}