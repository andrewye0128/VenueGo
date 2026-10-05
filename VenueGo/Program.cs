using Microsoft.AspNetCore.Authentication.Cookies; // Cookie 認證
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Extensions;   // 各子系統的服務註冊、API 文件
using VenueGo.Helpers;
using VenueGo.Services;
using VenueGo.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// 讀取每個人的本機設定
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true
);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
    o.InvalidModelStateResponseFactory = ApiResponses.InvalidModelState);

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
        options.Events.OnRedirectToLogin = ApiResponses.RedirectToLogin;
        options.Events.OnRedirectToAccessDenied = ApiResponses.RedirectToAccessDenied;
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

// ── 全組共用的服務 ───────────────────────────────────────
// 各子系統自己的服務不寫在這裡，寫在 Extensions/ 底下各自的檔案（見下方）。

// 目前登入者
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// HttpClient
builder.Services.AddHttpClient();   // 保留：組員可能有人用無名的 CreateClient()

// 自動校時（TimeAgo 等全站共用）
builder.Services.AddHttpClient(TimeService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ITimeService, TimeService>();
builder.Services.AddHostedService<TimeSyncHostedService>();

// ── 各子系統的服務（Extensions/*ModuleExtensions）────────────────
builder.Services.AddMemberModule();
builder.Services.AddVenueModule();
builder.Services.AddReservationModule(builder.Configuration);
builder.Services.AddTicketModule();
builder.Services.AddReviewModule(builder.Configuration);


// 註冊 Swagger 服務
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ── API 文件：OpenAPI＋Scalar（Extensions/ApiDocsExtensions）──────
builder.Services.AddApiDocs();

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

// API 文件（OpenAPI JSON＋Scalar 網頁）只在開發環境開啟
if (app.Environment.IsDevelopment())
{
    app.MapApiDocs();

    app.UseSwagger();
    app.UseSwaggerUI();
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

app.MapControllers();   // 讓 [Route("api/...")] 的 API 路由生效

app.Run();
