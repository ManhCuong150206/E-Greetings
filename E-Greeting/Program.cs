using E_Greeting.Controllers;
using E_Greeting.Models;
using E_Greeting.Service;
using E_Greeting.Service.Categories;
using E_Greeting.Service.Subscriptions;
using E_Greeting.Service.Templates;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Service for sending package expiration reminders via email VIP (Admin)
builder.Services.AddScoped<IAdminEmailService, AdminEmailService>();
builder.Services.AddScoped<ISubscriptionReminderService, SubscriptionReminderService>();
builder.Services.AddHostedService<SubscriptionReminderService>();

// Automated scheduled card-sending service (User)
builder.Services.AddHostedService<EmailSchedulerService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

// Add services to the container.
builder.Services.AddMemoryCache();

// REGISTER MVC CONTROLLERS WITH GLOBAL ADMIN ACCESS BLOCKER FILTER
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AdminAccessBlockerFilter>();
});

// Connect to SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// Authentication & Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();