using VenueGo.Models.CheckinModels;
using VenueGo.Models.DashboardModels;
using VenueGo.Services.CheckIn;
using VenueGo.Services.Ticket;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  票券與報到子系統的服務註冊（10/5 從 Program.cs 搬出來）
    //  Program.cs 只呼叫 builder.Services.AddTicketModule()；這個子系統新增服務時，只改這個檔案。
    //  全組共用的服務（登入者、HttpClient、校時⋯）留在 Program.cs，不放這裡。
    // ════════════════════════════════════════════════════════════════
    public static class TicketModuleExtensions
    {
        public static IServiceCollection AddTicketModule(this IServiceCollection services)
        {
            // 票券
            services.AddScoped<IEntryTicketService, EntryTicketService>();
            services.AddScoped<ITicketManualService, TicketManualService>();
            services.AddScoped<IMemberTicketQueryService, MemberTicketQueryService>();
            services.AddScoped<CCheckInLogFactory>();
            services.AddScoped<CTicketStatusLogFactory>();
            services.AddScoped<CTicketViewModelFactory>();

            // 報到，以及定時結算票券的背景工作
            services.AddScoped<ICheckInService, CheckInService>();
            services.AddHostedService<TicketSettlementHostedService>();

            // ⚠️ 歸屬待確認：場地即時使用狀況（查的是票券資料，所以先放這裡）
            services.AddScoped<VenueMonitorFactory>();

            return services;
        }
    }
}
