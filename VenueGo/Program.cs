using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Options;
using VenueGo.Services;
using VenueGo.Services.Members;
using VenueGo.Services.Reservations;
using VenueGo.Services.TimeSlots;
using VenueGo.Services.Venues;

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

// 註冊 Session
//builder.Services.AddSession();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


builder.Services.AddScoped<ITimeSlotService, TimeSlotService>();

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

// 註冊關於球館預約的業務邏輯的服務：介面 → 實作
builder.Services.Configure<ReservationRulesOptions>(
    builder.Configuration.GetSection(ReservationRulesOptions.SectionName));

builder.Services.AddScoped<IEntryTicketService, EntryTicketService>();

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
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    //pattern: "{controller=CReview}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
