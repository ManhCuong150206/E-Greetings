using E_Greeting.Models;
using E_Greeting.Service;
using Microsoft.EntityFrameworkCore;

public class AccountService : IAccountService
{
    private readonly AppDbContext _context;

    public AccountService(AppDbContext context) => _context = context;

    public async Task<(Account account, LoginResult result)> LoginAsync(string email, string password)
    {
        // Step 1: Find account by Email first, ignore password for now
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email);

        if (account == null)
            return (null, LoginResult.EmailNotFound);

        // Step 2: Email matches, now verify Password
        if (account.Password != password)
            return (null, LoginResult.WrongPassword);

        // Step 3: Both Email and Password are correct, now check account status (lock/active)
        if (!account.Status)
            return (null, LoginResult.AccountLocked);

        // Passed all 3 verification steps
        return (account, LoginResult.Success);
    }

    public async Task<(Account account, RegisterResult result)> RegisterAsync(string name, string email, string password)
    {
        var existingAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Email == email);

        if (existingAccount != null)
        {
            // Found existing account with this email -> disallow registration
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

        _context.Accounts.Add(newAccount);       // Stage new entity
        await _context.SaveChangesAsync();       // Commit changes to Database

        return (newAccount, RegisterResult.Success);
    }
}