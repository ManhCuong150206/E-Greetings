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
                Name = "Gói Cơ Bản (Basic)",
                Price = 0,
                Duration = "Miễn phí",
                Features = new List<string>
                {
                    "Gửi tối đa 5 thiệp/tháng",
                    "Sử dụng mẫu thiệp cơ bản",
                    "Gửi thiệp thủ công",

                }
            }
        };

        // Bước 1: Lấy dữ liệu thô về trước (chạy dưới SQL, không có Split/Trim ở đây)
        var rawServices = _context.Services
        .OrderBy(s => s.Price)
        .ToList();

        // Bước 2: Xử lý Split/Trim trên dữ liệu đã có sẵn trong bộ nhớ (thuần C#)
        var dbPlans = rawServices
            .Select(s => new SubscriptionModel
            {
                Id = s.Id,
                Name = s.Name,
                Price = s.Price,
                Duration = "tháng",
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