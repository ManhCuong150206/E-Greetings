using System;
using System.Collections.Generic;

namespace E_Greeting.Models
{
    public class UserManagementViewModel
    {
        public List<AccountUserDto> Users { get; set; } = new List<AccountUserDto>();
        public List<Service> Services { get; set; } = new List<Service>();
        public List<PaymentTransactionDto> Transactions { get; set; } = new List<PaymentTransactionDto>();

        public string ActiveTab { get; set; } = "users";
        public string? SearchUser { get; set; }
        public string? SearchTransaction { get; set; }
        public int? SelectedServiceId { get; set; }
        public string? SelectedStatus { get; set; }
    }

    public class AccountUserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Status { get; set; }
        public string CurrentVipPackage { get; set; } = "Miễn phí";
        public DateTime? VipEndDate { get; set; }
        public int SentCardsCount { get; set; }
    }

    public class PaymentTransactionDto
    {
        public int PaymentId { get; set; }
        public int SubscriptionId { get; set; }
        public int AccountId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime? VipStartDate { get; set; }
        public DateTime? VipEndDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}