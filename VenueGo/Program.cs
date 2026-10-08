using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies; // Cookie 認證
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using VenueGo.Data;
using VenueGo.Extensions;   // 各子系統的服務註冊、API 文件
using VenueGo.Helpers;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;
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
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Account/Login";              // 未登入時自動導向的頁面
//        options.AccessDeniedPath = "/Account/Login";       // 權限不足時導向的頁面
//        options.ExpireTimeSpan = TimeSpan.FromHours(8);    // Cookie 預設有效時間
//        options.Cookie.HttpOnly = true;                    // 防範 XSS 存取 Cookie
//        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 限定 HTTPS 傳輸
//    });

builder.Services.AddAuthentication(options =>
{
    // 預設仍然使用 Cookie
    // 保留原本 MVC 網頁的登入方式
    options.DefaultAuthenticateScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultSignInScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddJwtBearer(options =>
{
    // JWT 的 Secret Key
    var jwtKey = builder.Configuration["Jwt:Key"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        // 驗證 Token 是否真的由我們的系統簽發
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey!)
        ),

        // 驗證 Issuer
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        // 驗證 Audience
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],

        // 驗證 Token 是否過期
        ValidateLifetime = true,

        // 不需要額外增加時間容錯
        ClockSkew = TimeSpan.Zero
    };
});
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Account/Login";              // 未登入時自動導向的頁面
//        options.AccessDeniedPath = "/Account/Login";       // 權限不足時導向的頁面
//        options.ExpireTimeSpan = TimeSpan.FromHours(8);    // Cookie 預設有效時間
//        options.Cookie.HttpOnly = true;                    // 防範 XSS 存取 Cookie
//        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 限定 HTTPS 傳輸
//        options.Events.OnRedirectToLogin = ApiResponses.RedirectToLogin;
//        options.Events.OnRedirectToAccessDenied = ApiResponses.RedirectToAccessDenied;
//    });

// 註冊 Session
builder.Services.AddSession();

//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(30);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

// [重構搬移] 原本寫在 SettingController 裡的角色管理、員工帳號管理邏輯，
// 拆成這兩個 Service，Controller 現在只負責呼叫 + 轉換 ServiceResult 成對應的 View/Redirect。
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IEmployeeAccountService, EmployeeAccountService>();


builder.Services.AddScoped<IMemberAccountService, MemberAccountService>();

builder.Services.AddScoped<IUserProfileService, UserProfileService>();

// Service 層要讀寫 Session，需要透過 IHttpContextAccessor 取得 HttpContext
builder.Services.AddHttpContextAccessor();

// ── 全組共用的服務 ───────────────────────────────────────
// 各子系統自己的服務不寫在這裡，寫在 Extensions/ 底下各自的檔案（見下方）。

// 目前登入者
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// HttpClient
builder.Services.AddHttpClient();   // 保留：組員可能有人用無名的 CreateClient()

// 全站共用的時間來源，本地時區固定為台北
builder.Services.AddSingleton<TimeProvider, TaipeiTimeProvider>();

// 註冊關於時段方法的服務：介面 → 實作
//builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();

// 註冊關於時段選取驗證的服務：介面 → 實作
//builder.Services.AddScoped<ISlotSelectionValidator, SlotSelectionValidator>();

// 註冊關於預約計價的服務：介面 → 實作
//builder.Services.AddScoped<IReservationPricingService, ReservationPricingService>();

// 註冊關於目前登入者的服務：介面 → 實作
builder.Services.AddScoped<ICurrentUserService, VenueGo.Services.Auth.CurrentUserService>();

// 註冊會員登入驗證服務
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
// 註冊密碼重設服務
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
//註冊 IJwtService
builder.Services.AddScoped<IJwtService, JwtService>();

// 註冊關於訂單編號產生器的服務：介面 → 實作
//builder.Services.AddScoped<IOrderNoGenerator, OrderNoGenerator>();

// 註冊關於預約建立的服務：介面 → 實作
//builder.Services.AddScoped<IReservationCreationService, ReservationCreationService>();

// 註冊關於預約查詢與指令的服務：介面 → 實作
//builder.Services.AddScoped<IReservationQueryService, ReservationQueryService>();
// 註冊關於預約指令的服務：介面 → 實作
//builder.Services.AddScoped<IReservationCommandService, ReservationCommandService>();

// 註冊關於球館預約的業務邏輯的服務：介面 → 實作
//builder.Services.Configure<ReservationRulesOptions>(
    //builder.Configuration.GetSection(ReservationRulesOptions.SectionName));

// 註冊關於票券的服務：介面 → 實作
//builder.Services.AddScoped<IEntryTicketService, EntryTicketService>();
//builder.Services.AddScoped<IMemberTicketQueryService, MemberTicketQueryService>();
//builder.Services.AddScoped<CTicketViewModelFactory>();

//builder.Services.AddScoped<VenueMonitorFactory>();

// 評論系統使用
//builder.Services.AddScoped<ReviewTicketFactory>(); // 3者共用這個 ReviewTicketFactory 實例
//builder.Services.AddScoped<IVisitReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
//builder.Services.AddScoped<IBookingReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
// 自動校時使用
builder.Services.AddHttpClient();   // 保留：組員可能有人用無名的 CreateClient()
// 自動校時（TimeAgo 等全站共用）
builder.Services.AddHttpClient(TimeService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<ITimeService, TimeService>();
builder.Services.AddSingleton<ITimeMachine, TimeMachine>();   // 開發用時光機
builder.Services.AddHostedService<TimeSyncHostedService>();

// ── 各子系統的服務（Extensions/*ModuleExtensions）────────────────
builder.Services.AddMemberModule();
builder.Services.AddVenueModule();
builder.Services.AddReservationModule(builder.Configuration);
builder.Services.AddTicketModule();
builder.Services.AddReviewModule(builder.Configuration);


// 註冊 Swagger 服務
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "請輸入 JWT Token"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] =
                new List<string>()
        });
});

//builder.Services.AddScoped<ICurrentUser, FakeCurrentUser>();
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
