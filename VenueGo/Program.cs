using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Options;
using VenueGo.Models.ReviewModels;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;
using VenueGo.Services.Orders;
using VenueGo.Services.Reservations;
using VenueGo.Services.TimeSlots;
using VenueGo.Services.Venues;
using Microsoft.AspNetCore.Authentication.Cookies;
using VenueGo.Services.Ticket;
using VenueGo.Services.CheckIn; // [新增] 引入 Cookie 認證命名空間

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

//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

// Service 層要讀寫 Session，需要透過 IHttpContextAccessor 取得 HttpContext
builder.Services.AddHttpContextAccessor();

// 註冊關於會員方法的服務：介面 → 實作
builder.Services.AddScoped<IMemberQueryService, MemberQueryService>();

// 註冊關於訂位方法的服務：介面 → 實作
builder.Services.AddScoped<IReservationDraftStore, SessionReservationDraftStore>();

// 註冊關於場地方法的服務：介面 → 實作
builder.Services.AddScoped<IVenueQueryService, VenueQueryService>();

// 註冊關於時段方法的服務：介面 → 實作
builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();

// 註冊關於時段選取驗證的服務：介面 → 實作
builder.Services.AddScoped<ISlotSelectionValidator, SlotSelectionValidator>();

// 註冊關於預約計價的服務：介面 → 實作
builder.Services.AddScoped<IReservationPricingService, ReservationPricingService>();

// 註冊關於目前登入者的服務：介面 → 實作
builder.Services.AddScoped<ICurrentUserService, VenueGo.Services.Auth.CurrentUserService>();

// 註冊關於訂單編號產生器的服務：介面 → 實作
builder.Services.AddScoped<IOrderNoGenerator, OrderNoGenerator>();

// 註冊關於預約建立的服務：介面 → 實作
builder.Services.AddScoped<IReservationCreationService, ReservationCreationService>();

// 註冊關於預約查詢與指令的服務：介面 → 實作
builder.Services.AddScoped<IReservationQueryService, ReservationQueryService>();
// 註冊關於預約指令的服務：介面 → 實作
builder.Services.AddScoped<IReservationCommandService, ReservationCommandService>();

// 註冊關於球館預約的業務邏輯的服務：介面 → 實作
builder.Services.Configure<ReservationRulesOptions>(
    builder.Configuration.GetSection(ReservationRulesOptions.SectionName));

// 註冊關於票券的服務：介面 → 實作
builder.Services.AddScoped<IEntryTicketService, EntryTicketService>();

// 評論系統使用
builder.Services.AddScoped<ReviewTicketFactory>(); // 3者共用這個 ReviewTicketFactory 實例
builder.Services.AddScoped<IVisitReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
builder.Services.AddScoped<IBookingReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
// 自動校時使用
builder.Services.AddHttpClient();   // 保留：組員可能有人用無名的 CreateClient()
builder.Services.AddHttpClient(TimeService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ITimeService, TimeService>();
builder.Services.AddHostedService<TimeSyncHostedService>();

// 註冊關於票券報到的服務：介面 → 實作
builder.Services.AddScoped<ICheckInService, CheckInService>();

//builder.Services.AddScoped<ICurrentUser, FakeCurrentUser>();

var app = builder.Build();

// 在應用程式啟動時，將單例 TimeService 橋接給靜態類別
TimeAgo.TimeService = app.Services.GetRequiredService<ITimeService>();

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
