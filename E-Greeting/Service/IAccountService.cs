using E_Greeting.Models;

namespace E_Greeting.Service
{
    public interface IAccountService
    {
        Task<Account> LoginAsync(string email, string password);
    }
}
