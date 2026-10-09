using E_Greeting.Models;
using Microsoft.EntityFrameworkCore;

namespace E_Greeting.Service;

public class AccountService : IAccountService
{
    private readonly AppDbContext _context;

    public AccountService(AppDbContext context) => _context = context;

    public async Task<(Account?, LoginResult)> LoginAsync(string email, string password)
    {
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (account == null)
            return (null, LoginResult.EmailNotFound);

        // Kiểm tra mật khẩu bằng BCrypt (hỗ trợ fallback nếu tài khoản cũ chưa mã hóa)
        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(password, account.Password);
        }
        catch
        {
            isPasswordValid = (account.Password == password);
        }

        if (!isPasswordValid)
            return (null, LoginResult.WrongPassword);

        return (account, LoginResult.Success);
    }

    public async Task<(Account?, RegisterResult)> RegisterAsync(string name, string email, string password)
    {
        var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Email == email);
        if (existingAccount != null)
            return (null, RegisterResult.EmailExists);

        // Hash mật khẩu bằng BCrypt trước khi lưu DB
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var newAccount = new Account
        {
            Name = name,
            Email = email,
            Password = hashedPassword,
            Role = "User"
        };

        _context.Accounts.Add(newAccount);
        await _context.SaveChangesAsync();

        return (newAccount, RegisterResult.Success);
    }
}