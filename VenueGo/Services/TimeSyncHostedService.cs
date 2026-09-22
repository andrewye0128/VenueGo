namespace VenueGo.Services
{
    /// <summary>
    /// 背景校時服務：開機時校一次，之後每隔 TimeService.SyncInterval 再校一次。
    ///
    /// ── 為什麼要獨立一個背景服務 ─────────────────────────
    /// 如果「發現該校時了就順便校一下」寫在 Now 裡面，那個倒楣被選中的
    /// 請求就要多等一趟網路往返；API 掛掉時那個請求還會卡到逾時。
    /// 把它挪到背景執行緒，校時的成本就跟任何一個使用者都沒有關係了。
    ///
    /// ── BackgroundService 是什麼 ─────────────────────────
    /// ASP.NET Core 內建的「跟網站一起啟動、跟網站一起關閉」的長時間工作。
    /// 只要在 Program.cs 呼叫 AddHostedService，框架就會在網站啟動後
    /// 自己呼叫 ExecuteAsync，並在關站時透過 stoppingToken 通知它收工。
    /// </summary>
    public class TimeSyncHostedService(
        ITimeService timeService,
        ILogger<TimeSyncHostedService> logger) : BackgroundService
    {
        private readonly ITimeService _timeService = timeService;
        private readonly ILogger<TimeSyncHostedService> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("背景校時服務已啟動，間隔 {Interval}", TimeService.SyncInterval);

            // PeriodicTimer 是 .NET 6 之後的做法，比 Task.Delay 迴圈好在
            // 它不會把「這一輪工作花掉的時間」疊加到下一輪的間隔上。
            using var timer = new PeriodicTimer(TimeService.SyncInterval);

            // 先校一次，不然網站剛開的前 30 分鐘偏移量都是 0
            await SyncSafelyAsync(stoppingToken);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await SyncSafelyAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 關站時的正常結束，不是錯誤
            }

            _logger.LogInformation("背景校時服務已停止");
        }

        /// <summary>
        /// ⚠️ 這裡再包一層 try：背景服務裡「沒接到的例外」會把整個網站帶掉。
        ///    SyncAsync 自己已經吞掉所有例外了，這層是保險——
        ///    萬一哪天有人改壞了 SyncAsync，也不該讓整站陪葬。
        /// </summary>
        private async Task SyncSafelyAsync(CancellationToken stoppingToken)
        {
            try
            {
                await _timeService.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站要讓它往上傳，才能正常結束迴圈
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "背景校時發生未預期的例外");
            }
        }
    }
}
