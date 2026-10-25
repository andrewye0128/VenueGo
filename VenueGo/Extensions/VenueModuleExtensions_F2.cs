using VenueGo.Services.Venues;
using VenueGo.Services.VenueSchedules;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  場地子系統的服務註冊（10/5 從 Program.cs 搬出來）
    //  Program.cs 只呼叫 builder.Services.AddVenueModule()；這個子系統新增服務時，只改這個檔案。
    //  全組共用的服務（登入者、HttpClient、校時⋯）留在 Program.cs，不放這裡。
    // ════════════════════════════════════════════════════════════════
    public static class VenueModuleExtensions
    {
        public static IServiceCollection AddVenueModule(this IServiceCollection services)
        {
            // 場地查詢
            services.AddScoped<IVenueQueryService, VenueQueryService>();

            // 場地時段（場地模組提供：營業時段、不開放時段、尖峰、單價、可使用的場地）
            services.AddScoped<IVenueScheduleService, VenueScheduleService>();

            // 關閉時段（後台不開放時段管理：有預約時場館取消、取消票券、寄信通知會員）
            services.AddScoped<IVenueSlotClosingService, VenueSlotClosingService>();

            return services;
        }
    }
}
