namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約逾期的背景排程：每 1 分鐘呼叫一次 IReservationExpiryService。寫法比照 TicketSettlementHostedService。
    /// <para>
    /// 【為什麼不能直接注入 IReservationExpiryService】
    /// 背景服務整個網站只有一個，從開站活到關站（Singleton）；
    /// IReservationExpiryService 裡的 dbVenueContext 是「一個請求一份」（Scoped）。
    /// 直接注入的話會一直用同一個 DbContext，資料越來越舊、還可能出錯。
    /// 所以注入 IServiceScopeFactory，每一輪自己開一個新的 scope，再拿新的 Service。
    /// </para>
    /// <para>
    /// 【例外一定要接住】背景服務裡沒接住的例外，會讓整個網站停止（.NET 的預設行為）。
    /// 所以每一輪都用 try/catch 包起來，失敗只記 log，下一輪再試。
    /// </para>
    /// </summary>
    public class ReservationExpiryHostedService : BackgroundService
    {
        /// <summary>多久檢查一次。線上付款期限只有 50 分鐘，間隔要短，時段才會盡快釋放。</summary>
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationExpiryHostedService> _logger;

        public ReservationExpiryHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReservationExpiryHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("預約逾期排程已啟動，間隔 {Interval}", Interval);

            using var timer = new PeriodicTimer(Interval);

            try
            {
                // 開站先跑一次，不用等第一個 1 分鐘
                await RunOnceAsync(stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunOnceAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 關站時的正常結束，不是錯誤
            }

            _logger.LogInformation("預約逾期排程已停止");
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var expiryService = scope.ServiceProvider.GetRequiredService<IReservationExpiryService>();

                var count = await expiryService.ExpireDueAsync(stoppingToken);

                // 沒事做就不印，避免每分鐘洗版
                if (count > 0)
                {
                    _logger.LogInformation("本輪將 {Count} 筆預約改為已逾期", count);
                }
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站要往上傳，才能結束迴圈
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "預約逾期排程發生未預期的例外，下一輪再試");
            }
        }
    }
}