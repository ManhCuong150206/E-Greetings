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

    public async Task<(Account account, LoginResult result)> LoginAsync(string email, string password)
    {
        // Bước 1: Tìm theo Email trước, CHƯA quan tâm password
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email);

        if (account == null)
            return (null, LoginResult.EmailNotFound);

        // Bước 2: Email đúng rồi, giờ mới so Password
        if (account.Password != password)
            return (null, LoginResult.WrongPassword);

        // Bước 3: Email và Password đều đúng, giờ mới kiểm tra khóa
        if (!account.Status)
            return (null, LoginResult.AccountLocked);

        // Qua hết cả 3 bước
        return (account, LoginResult.Success);
    }

    public async Task<(Account account, RegisterResult result)> RegisterAsync(string name, string email, string password)
    {
        var existingAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email);

        if (existingAccount != null)
        {
            // Đã tìm thấy tài khoản dùng email này rồi → không cho đăng ký nữa
            return (null, RegisterResult.EmailExists);
        }


        var newAccount = new Account
        {
            Name = name,
            Email = email,
            Password = password,
            Role = "User",
            Status = true
        };

        _context.Accounts.Add(newAccount);       // bỏ vào "giỏ hàng", chưa vào DB
        await _context.SaveChangesAsync();       // ghi thật xuống DB

        return (newAccount, RegisterResult.Success);
    }
}