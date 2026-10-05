using VenueGo.Models.Options;
using VenueGo.Services.ReviewScreening;
using VenueGo.Services.ReviewTickets;

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

            // 評論預審：AI 設定、呼叫 AI 用的連線（30 秒逾時）、AI 服務、背景分析
            services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
            services.AddHttpClient(GeminiScreeningClient.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30));
            services.AddScoped<IReviewScreeningAi, GeminiScreeningClient>();
            services.AddScoped<IReviewScreeningService, ReviewScreeningService>();
            services.AddHostedService<ReviewScreeningHostedService>();

            return services;
        }
    }
}
