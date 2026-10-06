using VenueGo.Services.Members;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  會員子系統的服務註冊（10/5 從 Program.cs 搬出來）
    //  Program.cs 只呼叫 builder.Services.AddMemberModule()；這個子系統新增服務時，只改這個檔案。
    //  全組共用的服務（登入者、HttpClient、校時⋯）留在 Program.cs，不放這裡。
    // ════════════════════════════════════════════════════════════════
    public static class MemberModuleExtensions
    {
        public static IServiceCollection AddMemberModule(this IServiceCollection services)
        {
            // 會員查詢
            services.AddScoped<IMemberQueryService, MemberQueryService>();

            return services;
        }
    }
}
