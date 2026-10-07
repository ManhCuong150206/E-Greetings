using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace E_Greeting.Service.Subscriptions;

public class SubscriptionReminderService : BackgroundService, ISubscriptionReminderService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionReminderService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly HttpClient _httpClient = new();

    public SubscriptionReminderService(
        IServiceProvider serviceProvider,
        ILogger<SubscriptionReminderService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    // --- AUTOMATICALLY DETECT ACTIVE NGROK DOMAIN RUNNING IN TERMINAL ---
    private async Task<string> GetActiveNgrokDomainAsync()
    {
        try
        {
            // Ngrok defaults management API at http://127.0.0.1:4040/api/tunnels
            var response = await _httpClient.GetStringAsync("http://127.0.0.1:4040/api/tunnels");
            using var doc = JsonDocument.Parse(response);
            var tunnels = doc.RootElement.GetProperty("tunnels");

            foreach (var tunnel in tunnels.EnumerateArray())
            {
                string publicUrl = tunnel.GetProperty("public_url").GetString() ?? "";
                if (publicUrl.StartsWith("https://"))
                {
                    _logger.LogInformation($"[VIP Reminder] Automatically detected active Ngrok Domain running in Terminal: {publicUrl}");
                    return publicUrl;
                }
            }
        }
        catch
        {
            _logger.LogWarning("[VIP Reminder] Could not connect to local Ngrok API (or Ngrok is not running). Using Domain from appsettings.json.");
        }

        // If Ngrok is not running or fails, fallback to default Domain in appsettings.json
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

                _logger.LogInformation($"[VIP Reminder] Found {expiringSubs.Count} VIP subscriptions expiring on {targetDate:dd/MM/yyyy} with unsent emails.");

                // 1. AUTOMATICALLY GET ACTIVE NGROK DOMAIN FROM TERMINAL (Or keep appsettings if not running)
                string domain = await GetActiveNgrokDomainAsync();
                domain = domain.TrimEnd('/');

                // 2. ATTACH DIRECT LINK TO LOGIN PAGE WITH PARAM returnUrl=%2FSubscription
                string subscriptionUrl = $"{domain}/Account/Login?returnUrl=%2FSubscription";

                foreach (var sub in expiringSubs)
                {
                    if (sub.Account != null && !string.IsNullOrEmpty(sub.Account.Email))
                    {
                        string serviceName = sub.Service?.Name ?? "VIP";
                        string subject = "[E-Greeting] Notice: Your VIP subscription is expiring soon!";

                        string refCode = DateTime.Now.ToString("yyyyMMddHHmmss");

                        string body = $@"
                                <div style='font-family: Arial, sans-serif; padding: 25px; border: 1px solid #e0e0e0; border-radius: 12px; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                                    <h3 style='color: #0d6efd; margin-top: 0;'>Hello {sub.Account.Name},</h3>
                                    <p style='font-size: 15px; color: #333; line-height: 1.5;'>Your <b>{serviceName}</b> subscription plan at <b>E-Greeting</b> will expire on <b>{sub.EndDate:dd/MM/yyyy}</b> (in 3 days).</p>
                                    <p style='font-size: 15px; color: #333; line-height: 1.5;'>To keep creating unique greeting cards without interruption, please renew your subscription soon!</p>
                                    
                                    <div style='margin: 25px 0;'>
                                        <a href='{subscriptionUrl}' style='padding: 12px 28px; background-color: #ffc107; color: #000000; text-decoration: none; border-radius: 25px; font-weight: bold; font-size: 15px; display: inline-block;'>Renew Now</a>
                                    </div>
                                    
                                    <p style='font-size: 12px; color: #6c757d;'>Or access directly via link: <a href='{subscriptionUrl}' style='color: #0d6efd;'>{subscriptionUrl}</a></p>

                                    <hr style='margin-top: 25px; border: none; border-top: 1px solid #eeeeee;' />
                                    <p style='font-size: 12px; color: #777777; margin-bottom: 5px;'>Best regards,<br/>The E-Greeting Team</p>
                                    <span style='font-size: 10px; color: #ffffff; display: none;'>Ref: {refCode}</span>
                                </div>";

                        try
                        {
                            await adminEmailService.SendAdminNotificationEmailAsync(sub.Account.Email, subject, body);

                            sub.IsReminderSent = true;
                            _logger.LogInformation($"[VIP Reminder] Successfully sent reminder email for plan '{serviceName}' to User: {sub.Account.Email}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"[VIP Reminder] Error sending email to {sub.Account.Email}");
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
                _logger.LogError(ex, "Error running background service for VIP expiration reminder emails.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}