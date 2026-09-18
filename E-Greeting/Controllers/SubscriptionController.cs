using Microsoft.AspNetCore.Mvc;

namespace E_Greeting.Controllers;

public class SubscriptionModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Duration { get; set; } = "tháng";
    public List<string> Features { get; set; } = new();
    public bool IsPopular { get; set; } = false;
}

public class SubscriptionController : Controller
{
    // GET: /Subscription
    public IActionResult Index()
    {
        var plans = new List<SubscriptionModel>
        {
            new() {
                Id = 1,
                Name = "Gói Cơ Bản (Basic)",
                Price = 0,
                Duration = "Miễn phí",
                Features = new List<string>
                {
                    "Gửi tối đa 5 thiệp/tháng",
                    "Sử dụng mẫu thiệp cơ bản",
                    "Đính kèm Logo E-Greeting",
                    "Hỗ trợ qua Email"
                },
                IsPopular = false
            },
            new() {
                Id = 2,
                Name = "Gói Cao Cấp (Premium)",
                Price = 99000,
                Duration = "tháng",
                Features = new List<string>
                {
                    "Gửi thiệp KHÔNG GIỚI HẠN",
                    "Mở khóa toàn bộ mẫu VIP",
                    "Tự động gửi thiệp theo lịch hẹn",
                    "Xóa bỏ Logo / Watermark",
                    "Hỗ trợ ưu tiên 24/7"
                },
                IsPopular = true
            },
            new() {
                Id = 3,
                Name = "Gói Doanh Nghiệp (Pro)",
                Price = 299000,
                Duration = "tháng",
                Features = new List<string>
                {
                    "Tất cả tính năng gói Premium",
                    "Chèn logo thương hiệu riêng",
                    "Gửi thiệp hàng loạt qua danh sách Email",
                    "Báo cáo chi tiết lượt mở thiệp",
                    "Hỗ trợ kỹ thuật riêng 1-1"
                },
                IsPopular = false
            }
        };

        return View(plans);
    }

    // POST: /Subscription/Subscribe
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Subscribe(int planId)
    {
        if (User.Identity != null && !User.Identity.IsAuthenticated)
        {
            TempData["ErrorMessage"] = "Bạn vui lòng đăng nhập trước khi đăng ký gói dịch vụ!";
            return RedirectToAction("Login", "Account");
        }

        // TODO: Kết nối CSDL để lưu Subscriber/Subscription tại đây nếu cần

        TempData["SuccessMessage"] = "Đăng ký gói dịch vụ thành công! Cảm ơn bạn đã tin tưởng E-Greeting.";
        return RedirectToAction("Index");
    }
}