using E_Greeting.Models;
using E_Greeting.Service;
using Microsoft.EntityFrameworkCore;

public class AccountService : IAccountService
{
    private readonly AppDbContext _context;

    public AccountService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Account> LoginAsync(string email, string password)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email && a.Password == password);
        // so sánh trực tiếp: email khớp VÀ password khớp y chang chuỗi trong DB

        if (account == null) return null;   // sai email hoặc sai password
        if (!account.Status) return null;    // tài khoản bị khóa

        return account;
    }
}