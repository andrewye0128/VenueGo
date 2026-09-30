namespace VenueGo.Services.CheckIn
{
    /// <summary>
    /// 背景結算服務:每隔一段時間掃一次 Valid/Used 的票券,把過了預約結束時間的批次結算掉
    /// (未使用失效 / 超時失效 / 已完成)。寫法比照 Services/TimeSyncHostedService.cs。
    ///
    /// ── 為什麼不能直接建構子注入 ICheckInService ─────────────────────
    /// ICheckInService 是 Scoped(內部依賴 dbVenueContext,DbContext 一定是 Scoped)。
    /// 這個 HostedService 本身是 Singleton 生命週期,Singleton 直接建構子注入 Scoped
    /// 服務會抓到過期/共用的 DbContext(captive dependency),所以改注入
    /// IServiceScopeFactory,每次 tick 自己開一個 scope 再去要 ICheckInService。
    /// </summary>
    public class TicketSettlementHostedService(
        // ASP.NET Core Host 啟動時框架自己內建注入
        IServiceScopeFactory scopeFactory,
        // log方法 -> 讓 log 系統知道「這則訊息是哪個類別印出來的」
        ILogger<TicketSettlementHostedService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<TicketSettlementHostedService> _logger = logger;


        // 複寫Microsoft.Extensions.Hosting抽象類別(繼承都要寫這支方法的內容), CancellationToken用來傳遞結束的「取消訊號」
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("背景票券結算服務已啟動,間隔 {Interval}", Interval);

            using var timer = new PeriodicTimer(Interval);

            // 開機先跑一次,不然要等 30 分鐘第一輪才結算
            await SettleSafelyAsync(stoppingToken);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SettleSafelyAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 關站時的正常結束,不是錯誤
            }

            _logger.LogInformation("背景票券結算服務已停止");
        }

        private async Task SettleSafelyAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var checkInService = scope.ServiceProvider.GetRequiredService<ICheckInService>();
                var count = await checkInService.SettleAllDueTicketsAsync();
                _logger.LogInformation("本輪結算了 {Count} 張票券", count);
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站要讓它往上傳,才能正常結束迴圈
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "背景票券結算發生未預期的例外");
            }
        }
    }
}
