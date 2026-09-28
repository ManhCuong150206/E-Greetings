using E_Greeting.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace E_Greeting.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhook/sepay")]
public class SePayWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public SePayWebhookController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public class SePayPayload
    {
        public long id { get; set; }
        public string? content { get; set; }
        public string? transferType { get; set; }
        public decimal transferAmount { get; set; }
    }

    [HttpPost]
    public IActionResult Receive([FromBody] SePayPayload data)
    {
        var expected = $"Apikey {_config["SePay:ApiKey"]}";
        var actual = Request.Headers["Authorization"].ToString();
        if (actual != expected)
            return Unauthorized(new { success = false });

        if (data.transferType != "in" || string.IsNullOrEmpty(data.content))
            return Ok(new { success = true });

        var match = Regex.Match(data.content, @"DH(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int paymentId))
            return Ok(new { success = true });

        var payment = _context.Payments
            .Include(p => p.Subscription)
            .FirstOrDefault(p => p.Id == paymentId && p.Status == "UNPAID");

        if (payment != null && data.transferAmount >= payment.Amount)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            payment.Status = "PAID";
            payment.PaymentDate = today;
            payment.Subscription.IsActive = true;
            payment.Subscription.StartDate = today;
            payment.Subscription.EndDate = today.AddMonths(1);
            _context.SaveChanges();
        }

        return Ok(new { success = true });
    }
}