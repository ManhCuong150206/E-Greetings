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
                _logger.LogError(ex, "An error occurred inside EmailSchedulerService");
            }

            // Scan database every 60 seconds
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task ProcessPendingEmailsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        DateTime now = DateTime.Now;

        // FETCH CARDS: Not sent yet (SentAt == null) + Scheduled (ScheduledAt != null) + Due time reached (ScheduledAt <= now)
        var pendingCards = await context.SentCards
            .Include(c => c.Account)
            .Where(c => c.SentAt == null
                     && c.ScheduledAt.HasValue
                     && c.ScheduledAt.Value <= now)
            .ToListAsync();

        if (!pendingCards.Any()) return;

        // Get configured domain & automatically remove any trailing slashes
        string rawDomain = configuration["AppSettings:Domain"] ?? "https://maritime-ripcord-crummiest.ngrok-free.dev";
        string domain = rawDomain.TrimEnd('/');

        foreach (var card in pendingCards)
        {
            try
            {
                string cardViewUrl = $"{domain}/Card/ViewCard/{card.Id}";
                string senderName = card.Account?.Name ?? "E-Greeting System";

                await emailService.SendCardEmailAsync(card.RecipientEmail, card.Subject, cardViewUrl, senderName);

                // Update SentAt timestamp upon successful email delivery
                card.SentAt = DateTime.Now;
                context.SentCards.Update(card);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending scheduled email for Card ID: {card.Id}");
            }
        }

        await context.SaveChangesAsync();
    }
}