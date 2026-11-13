using Microsoft.AspNetCore.Authentication.JwtBearer;
using VenueGo.Hubs;
using VenueGo.Options;
using VenueGo.Services.Notifications;
using VenueGo.Services.Orders;
using VenueGo.Services.Payments;
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

            // 綠界金流的設定（appsettings.Local.json 的 Ecpay；沒設定也能啟動，只是不能線上付款）
            services.Configure<EcpayOptions>(configuration.GetSection(EcpayOptions.SectionName));

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

            // 前台場地預約頁：查某天各場地的時段、每天剩幾格
            services.AddScoped<IBookingAvailabilityService, BookingAvailabilityService>();

            // 前台會員預約：試算、建立預約（寫入交給 IReservationCreationService.WriteAsync）
            services.AddScoped<IMemberBookingService, MemberBookingService>();

            // 前台會員「訂單與付款」：訂單列表、明細
            services.AddScoped<IMemberOrderQueryService, MemberOrderQueryService>();

            // 收款確認：後台標記已付款、綠界付款成功共用（改狀態、建票券、逾期後收款的處理）
            services.AddScoped<IPaymentConfirmationService, PaymentConfirmationService>();

            // 開始線上付款：檢查能不能付、付款改成處理中、產生綠界付款單
            services.AddScoped<IEcpayCheckoutService, EcpayCheckoutService>();

            // 主動問綠界付款結果（QueryTradeInfo）：收到失敗通知時確認、3-C-5 排程也會用。連線 10 秒逾時
            services.AddHttpClient(EcpayTradeQueryService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
            services.AddScoped<IEcpayTradeQueryService, EcpayTradeQueryService>();

            // 綠界付款結果：記錄和綠界的每一次往來（PaymentTransactions）、處理伺服器通知和瀏覽器帶回
            services.AddScoped<IPaymentTransactionRecorder, PaymentTransactionRecorder>();

            // 付款確認員：綠界說付了→收款；問綠界→依回答處理（通知、排程對帳、開始付款共用）
            services.AddScoped<IEcpayPaymentVerifier, EcpayPaymentVerifier>();
            services.AddScoped<IEcpayNotificationService, EcpayNotificationService>();

            // 逾期排程：每 10 秒把超過付款期限、還沒付款的預約改成已逾期，並釋放時段
            services.AddScoped<IReservationExpiryService, ReservationExpiryService>();

            // 預約逾期背景排程
            services.AddHostedService<ReservationExpiryHostedService>();

            // 付款對帳：主動問綠界卡在「付款處理中」的付款（3-C-5）
            services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();

            // 付款對帳背景排程：每 1 分鐘一輪
            services.AddHostedService<PaymentReconciliationHostedService>();

            // 前台會員的即時通知（SignalR）：逾期排程改完狀態後，通知會員的瀏覽器重新讀取
            // MemberNotifier 只用到 IHubContext（整個網站一份），所以註冊成 Singleton
            services.AddSignalR();
            services.AddSingleton<IMemberNotifier, MemberNotifier>();
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, AllowHubAccessTokenFromQuery);

            return services;
        }

        /// <summary>
        /// 讓會員連即時通知時，JWT 可以放在網址的 ?access_token=。
        /// <para>
        /// 【為什麼】瀏覽器建立 WebSocket 時沒辦法自訂標頭，SignalR 的做法是把 token 放在網址上（微軟官方文件的做法）。
        /// </para>
        /// <para>
        /// 【為什麼用 PostConfigure】JWT 的設定在 Program.cs（全組共用）。PostConfigure 會在 Program.cs 的設定之後
        /// 再補上這一段，Program.cs 的 JWT 區塊不用改。
        /// </para>
        /// <para>
        /// 【安全】只有即時通知的網址才從網址讀 token，其他 API 一律只看 Authorization 標頭；
        /// 標頭已經有 token 時也不看網址。連線一律是 HTTPS（wss://），網址在傳送途中是加密的。
        /// 注意：不要開啟會記錄完整網址的請求 log，token 會被寫進 log。
        /// </para>
        /// </summary>
        private static void AllowHubAccessTokenFromQuery(JwtBearerOptions options)
        {
            options.Events ??= new JwtBearerEvents();

            // 保留原本的處理（目前沒有，預設什麼都不做），以後 Program.cs 加了也不會被蓋掉
            var original = options.Events.OnMessageReceived;

            options.Events.OnMessageReceived = async context =>
            {
                await original(context);

                if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Path.StartsWithSegments(MemberNotificationHub.Path)
                    && context.Request.Query.TryGetValue("access_token", out var accessToken))
                {
                    context.Token = accessToken;
                }
            };
        }

        /// <summary>
        /// 掛上前台會員的即時通知（MemberNotificationHub）。
        /// Program.cs 在 app.MapControllers() 之後呼叫：要放在 UseAuthentication、UseAuthorization 之後，登入檢查才有效。
        /// </summary>
        public static IEndpointRouteBuilder MapMemberNotifications(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<MemberNotificationHub>(MemberNotificationHub.Path);
            return endpoints;
        }
    }
}
