namespace VenueGo.Services.ReviewScreening
{
    /// <summary>
    /// 評論預審的背景工作：每 30 秒撈一次「待分析」或「到了重試時間」的預審紀錄，送給 AI 分析。
    /// 寫法比照 TicketSettlementHostedService（每一輪自己開一個 scope，理由見那支檔案的說明）。
    ///
    /// ── 沒有設定金鑰時整個停用 ─────────────────────────────
    /// 組員的電腦沒有 Gemini 金鑰，他們的資料庫也沒有預審的兩張表。
    /// 啟動時檢查一次，沒設定就寫一行 log 然後結束，之後完全不碰資料庫。
    /// </summary>
    public sealed class ReviewScreeningHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReviewScreeningHostedService> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 每一輪最多處理幾則。一則大約要 5～10 秒；一次處理太多，
        /// 容易撞到免費額度的每分鐘上限，而且關站時要等很久才停得下來。
        /// </summary>
        private const int BatchSize = 5;

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<ReviewScreeningHostedService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IReviewScreeningService>();
                if (!service.IsEnabled)
                {
                    _logger.LogInformation("評論預審未設定 Gemini 金鑰或模型，背景分析不啟動");
                    return;
                }
            }

            _logger.LogInformation("評論預審背景分析已啟動，間隔 {Interval}", Interval);

            using var timer = new PeriodicTimer(Interval);
            try
            {
                // 開機先跑一次，再每隔一段時間跑一次
                do
                {
                    await RunOnceSafelyAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                // 關站時的正常結束，不是錯誤
            }

            _logger.LogInformation("評論預審背景分析已停止");
        }

        private async Task RunOnceSafelyAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IReviewScreeningService>();
                int count = await service.ProcessDueAsync(BatchSize, stoppingToken);

                // 沒事做的時候不寫，不然每 30 秒一行，真正的訊息會被淹沒
                if (count > 0)
                    _logger.LogInformation("本輪處理了 {Count} 則評論預審", count);
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站要讓它往上傳，才能正常結束迴圈
            }
            catch (Exception ex)
            {
                // 一輪出錯不能讓整個背景工作停掉，下一輪再試
                _logger.LogError(ex, "評論預審背景分析發生未預期的例外");
            }
        }
    }
}
