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
                await ProcessPendingEmailsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong EmailSchedulerService");
            }

            // Quét CSDL mỗi 10 giây
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessPendingEmailsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        DateTime now = DateTime.Now;

        // CHỈ LẤY THIỆP: Chưa gửi (SentAt == null) + CÓ hẹn giờ (ScheduledAt != null) + ĐÃ ĐẾN GIỜ (ScheduledAt <= now)
        var pendingCards = await context.SentCards
            .Include(c => c.Account)
            .Where(c => c.SentAt == null
                     && c.ScheduledAt.HasValue
                     && c.ScheduledAt.Value <= now)
            .ToListAsync();

        if (!pendingCards.Any()) return;

        string domain = configuration["AppSettings:Domain"] ?? "https://localhost:7130";

        foreach (var card in pendingCards)
        {
            try
            {
                string cardViewUrl = $"{domain}/Card/ViewCard/{card.Id}";
                string senderName = card.Account?.Name ?? "Hệ thống E-Greeting";

                await emailService.SendCardEmailAsync(card.RecipientEmail, card.Subject, cardViewUrl, senderName);

                // Gửi xong mới cập nhật SentAt
                card.SentAt = DateTime.Now;
                context.SentCards.Update(card);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi gửi email cho Card ID: {card.Id}");
            }
        }

        await context.SaveChangesAsync();
    }
}