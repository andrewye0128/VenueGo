using Microsoft.AspNetCore.Authentication.Cookies; // [新增] 引入 Cookie 認證命名空間
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Services;
using VenueGo.Models.ReviewModels;

var builder = WebApplication.CreateBuilder(args);

// 讀取每個人的本機設定
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true
);

// Add services to the container.
builder.Services.AddControllersWithViews();


// 註冊 EF Core DbContext
builder.Services.AddDbContext<dbVenueContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ==========================================
// 1. [新增] 註冊 Cookie 身份認證服務
// ==========================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";              // 未登入時自動導向的頁面
        options.AccessDeniedPath = "/Account/Login";       // 權限不足時導向的頁面
        options.ExpireTimeSpan = TimeSpan.FromHours(8);    // Cookie 預設有效時間
        options.Cookie.HttpOnly = true;                    // 防範 XSS 存取 Cookie
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 限定 HTTPS 傳輸
    });

// 註冊 Session
builder.Services.AddSession();

builder.Services.AddScoped<IEntryTicketService, EntryTicketService>();

// 評論系統使用
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddScoped<ReviewTicketFactory>(); // 3者共用這個 ReviewTicketFactory 實例
builder.Services.AddScoped<IVisitReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
builder.Services.AddScoped<IBookingReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
//啟動 Sesion
app.UseSession();

// ==========================================
// 2. [新增] 啟用身份驗證 Middleware (必須放在 UseAuthorization 之前)
// ==========================================
app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
