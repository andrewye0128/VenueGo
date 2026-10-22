using Microsoft.Extensions.Options;
using VenueGo.Options;
using VenueGo.Services.Reviews;
using VenueGo.Services.ReviewScreening;
using VenueGo.Services.ReviewTickets;
using VenueGo.Services.Weather;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  評論子系統的服務註冊（10/5 從 Program.cs 搬出來）
    //  Program.cs 只呼叫 builder.Services.AddReviewModule(builder.Configuration)；這個子系統新增服務時，只改這個檔案。
    //  全組共用的服務（登入者、HttpClient、校時⋯）留在 Program.cs，不放這裡。
    // ════════════════════════════════════════════════════════════════
    public static class ReviewModuleExtensions
    {
        public static IServiceCollection AddReviewModule(this IServiceCollection services, IConfiguration configuration)
        {
            // 評論憑證工廠：報到組、訂單組、評論頁三方共用同一個實例
            services.AddScoped<ReviewTicketFactory>();
            services.AddScoped<IVisitReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
            services.AddScoped<IBookingReviewTicketFactory>(sp => sp.GetRequiredService<ReviewTicketFactory>());
            services.AddScoped<IReviewTicketBackfill>(sp => sp.GetRequiredService<ReviewTicketFactory>());   // 「我的評論」補建漏掉的憑證（10/11）

            // 評論資格判定：撰寫、送出、我的評論共用同一套規則（10/9）
            services.AddScoped<IReviewEligibilityService, ReviewEligibilityService>();

            // 會員自己的評論、評論專區查詢：10/10 從 CReviewApiController 抽出來
            services.AddScoped<IMemberReviewService, MemberReviewService>();
            services.AddScoped<IPublicReviewQueryService, PublicReviewQueryService>();

            // 評論預審：AI 設定、呼叫 AI 用的連線（30 秒逾時）、AI 服務、背景分析
            services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
            services.AddHttpClient(GeminiScreeningClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
            services.AddScoped<IReviewScreeningAi, GeminiScreeningClient>();
            services.AddScoped<IReviewScreeningService, ReviewScreeningService>();
            services.AddHostedService<ReviewScreeningHostedService>();

            // 首頁 TopBar 即時氣象（10/18，F5 的功能，不屬於評論，但同一個子系統所以放這裡）：
            // 氣象署設定（授權碼在 Secrets.json）、連線（5 秒逾時，氣象署慢就先不顯示）、快取、服務
            services.Configure<CwaOptions>(configuration.GetSection(CwaOptions.SectionName));
            services.AddHttpClient(WeatherService.HttpClientName, (sp, c) =>
            {
                c.BaseAddress = new Uri(sp.GetRequiredService<IOptions<CwaOptions>>().Value.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(5);
            });
            services.AddMemoryCache();   // 已經註冊過也沒關係，不會重複
            services.AddScoped<IWeatherService, WeatherService>();

            return services;
        }
    }
}
