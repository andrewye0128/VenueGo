using VenueGo.Models.Options;
using VenueGo.Services.Orders;
using VenueGo.Services.Reservations;
using VenueGo.Services.TimeSlots;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  預約與訂單子系統的服務註冊（10/5 從 Program.cs 搬出來）
    //  Program.cs 只呼叫 builder.Services.AddReservationModule(builder.Configuration)；這個子系統新增服務時，只改這個檔案。
    //  全組共用的服務（登入者、HttpClient、校時⋯）留在 Program.cs，不放這裡。
    // ════════════════════════════════════════════════════════════════
    public static class ReservationModuleExtensions
    {
        public static IServiceCollection AddReservationModule(this IServiceCollection services, IConfiguration configuration)
        {
            // 球館預約的業務規則（appsettings.json 的 ReservationRules）
            services.Configure<ReservationRulesOptions>(configuration.GetSection(ReservationRulesOptions.SectionName));

            // 訂位草稿（存在 Session）
            services.AddScoped<IReservationDraftStore, SessionReservationDraftStore>();

            // ⚠️ 歸屬待確認：時段服務原本寫在場地服務旁邊，看用途比較像預約在用
            services.AddScoped<ITimeSlotService, TimeSlotService>();

            // 時段選取驗證、預約計價、訂單編號
            services.AddScoped<ISlotSelectionValidator, SlotSelectionValidator>();
            services.AddScoped<IReservationPricingService, ReservationPricingService>();
            services.AddScoped<IOrderNoGenerator, OrderNoGenerator>();

            // 預約的建立、查詢、指令
            services.AddScoped<IReservationCreationService, ReservationCreationService>();
            services.AddScoped<IReservationQueryService, ReservationQueryService>();
            services.AddScoped<IReservationCommandService, ReservationCommandService>();

            return services;
        }
    }
}
