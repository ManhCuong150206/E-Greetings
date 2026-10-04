using E_Greeting.Models;
using E_Greeting.Service;
using E_Greeting.Service.Categories;
using E_Greeting.Service.Subscriptions;
using E_Greeting.Service.Templates;
using E_Greeting.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Services
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

// Đăng ký Service Nhắc hết hạn gói VIP (Feature tự động gửi mail ngầm)
builder.Services.AddScoped<IAdminEmailService, AdminEmailService>();
builder.Services.AddScoped<ISubscriptionReminderService, SubscriptionReminderService>();
builder.Services.AddHostedService<SubscriptionReminderService>();

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
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews();

// Kết nối SQL Server
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

// Middleware xác thực & phân quyền
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