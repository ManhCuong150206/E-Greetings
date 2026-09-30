using System;
using System.Collections.Generic;

namespace E_Greeting.Models
{
    public class DashboardViewModel
    {
        public int TotalTemplates { get; set; }
        public int TotalCategories { get; set; }
        public int TotalUsers { get; set; }
        public int TotalSentCards { get; set; }
        public decimal TotalRevenue { get; set; }

        // Danh sách thống kê chi tiết theo từng Gói dịch vụ
        public List<ServiceStatDto> ServiceStats { get; set; } = new List<ServiceStatDto>();

        public List<RecentPaymentDto> RecentPayments { get; set; } = new List<RecentPaymentDto>();
        public decimal[] MonthlyRevenue { get; set; } = new decimal[12];
    }

    public class ServiceStatDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TotalSubscribers { get; set; } // Số lượt đăng ký thành công
        public decimal TotalRevenue { get; set; }   // Doanh thu riêng của gói này
    }

    public class RecentPaymentDto
    {
        public int PaymentId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}