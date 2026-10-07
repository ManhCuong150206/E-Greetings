using E_Greeting.Controllers;
using E_Greeting.Models;
using E_Greeting.Service;

public class ServiceService : IServiceService
{
    private readonly AppDbContext _context;

    public ServiceService(AppDbContext context) => _context = context;

    public List<SubscriptionModel> GetAllPlans()
    {
        var plans = new List<SubscriptionModel>
        {
            new()
            {
                Id = 0,
                Name = "Basic Plan",
                Price = 0,
                Duration = "Free",
                Features = new List<string>
                {
                    "Send up to 5 cards/month",
                    "Use standard card templates",
                    "Send cards manually"
                }
            }
        };

        // Step 1: Retrieve raw data first (executes in SQL, no Split/Trim here)
        var rawServices = _context.Services
            .OrderBy(s => s.Price)
            .ToList();

        // Step 2: Perform Split/Trim on in-memory data (pure C#)
        var dbPlans = rawServices
            .Select(s => new SubscriptionModel
            {
                Id = s.Id,
                Name = s.Name,
                Price = s.Price,
                Duration = "month",
                Features = s.Features != null
                    ? s.Features.Split('-', StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim())
                                .ToList()
                    : new List<string>()
            })
            .ToList();

        plans.AddRange(dbPlans);
        return plans;
    }
}