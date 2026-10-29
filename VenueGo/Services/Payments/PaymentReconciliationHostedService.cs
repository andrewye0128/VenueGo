using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 付款對帳的背景排程（3-C-5）：每 1 分鐘呼叫一次 IPaymentReconciliationService。
    /// 寫法同 ReservationExpiryHostedService：
    /// <para>
    /// 【每一輪開新的 scope】背景服務整個網站只有一個（Singleton），對帳服務裡的 DbContext 是一個請求一份（Scoped），
    /// 所以每一輪自己開一個新的 scope，再拿新的 Service
    /// </para>
    /// <para>
    /// 【例外一定要接住】背景服務沒接住的例外會讓整個網站停止，所以每一輪都包 try/catch
    /// </para>
    /// <para>
    /// 每分鐘跑一次不代表每分鐘都問綠界：哪些付款該問，由對帳服務依設定（送出 10 分鐘後、隔 10 分鐘再問）決定
    /// </para>
    /// </summary>
    public class PaymentReconciliationHostedService : BackgroundService
    {
        /// <summary>多久檢查一次。跟逾期排程一樣：改回未付款之後，時段要盡快釋放</summary>
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaymentReconciliationHostedService> _logger;

        public PaymentReconciliationHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentReconciliationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("付款對帳排程已啟動，間隔 {Interval}", Interval);

            using var timer = new PeriodicTimer(Interval);

            try
            {
                // 開站先跑一次：網站關著的期間卡住的付款，不用等第一個 1 分鐘
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

            _logger.LogInformation("付款對帳排程已停止");
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciliation = scope.ServiceProvider.GetRequiredService<IPaymentReconciliationService>();

                var decisions = await reconciliation.ReconcileAsync(stoppingToken);

                // 沒事做就不印，避免每分鐘洗版
                if (decisions.Count > 0)
                {
                    _logger.LogInformation(
                        "本輪付款對帳問了 {Count} 筆：{Summary}", decisions.Count, Summarize(decisions));
                }
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站要往上傳，才能結束迴圈
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "付款對帳排程發生未預期的例外，下一輪再試");
            }
        }

        /// <summary>例如「Confirmed 1、RevertedToUnpaid 2」</summary>
        private static string Summarize(IEnumerable<EcpayPaymentDecision> decisions)
            => string.Join("、", decisions
                .GroupBy(d => d.Outcome)
                .Select(g => $"{g.Key} {g.Count()}"));
    }
}