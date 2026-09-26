using E_Greeting.Models;

namespace E_Greeting.Service
{
    public interface IAccountService
    {
        Task<(Account account, LoginResult result)> LoginAsync(string email, string password);
        Task<(Account account, RegisterResult result)> RegisterAsync(string name, string email, string password);
    }
}