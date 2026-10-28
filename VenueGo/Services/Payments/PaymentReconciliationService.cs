using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Options;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 付款對帳的實作。
    /// <para>
    /// 【只負責挑】哪些付款該問了（送出夠久、最近沒問過），問完怎麼處理交給付款確認員（IEcpayPaymentVerifier），
    /// 跟收到通知、開始付款用同一套規則
    /// </para>
    /// <para>
    /// 【不要問太快】綠界問太快會擋 30 分鐘：每輪最多 MaxQueriesPerRound 筆、每筆間隔 DelayBetweenQueries、
    /// 同一筆隔 RequeryInterval 才再問；被擋了就停止這一輪
    /// </para>
    /// <para>
    /// 【一筆一筆處理】寫法同 ReservationExpiryService：一筆失敗不影響其他筆，下一輪再試
    /// </para>
    /// </summary>
    public class PaymentReconciliationService : IPaymentReconciliationService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _timeService;
        private readonly EcpayOptions _ecpay;
        private readonly IEcpayPaymentVerifier _verifier;
        private readonly ILogger<PaymentReconciliationService> _logger;

        public PaymentReconciliationService(
            dbVenueContext db,
            ITimeService timeService,
            IOptionsSnapshot<EcpayOptions> ecpay,
            IEcpayPaymentVerifier verifier,
            ILogger<PaymentReconciliationService> logger)
        {
            _db = db;
            _timeService = timeService;
            _ecpay = ecpay.Value;
            _verifier = verifier;
            _logger = logger;
        }

        private EcpayReconciliationOptions Rules => _ecpay.Reconciliation;

        public async Task<IReadOnlyList<EcpayPaymentDecision>> ReconcileAsync(
            CancellationToken cancellationToken = default)
        {
            // 沒設定綠界的電腦（組員）沒有線上付款，也就不會有處理中的付款，不用問
            if (!_ecpay.IsConfigured)
            {
                return [];
            }

            var candidates = await FindCandidatesAsync(_timeService.Now, cancellationToken);

            var decisions = new List<EcpayPaymentDecision>();
            foreach (var candidate in candidates)
            {
                // 第一筆不用等，之後每筆間隔一下
                if (decisions.Count > 0)
                {
                    await Task.Delay(Rules.DelayBetweenQueries, cancellationToken);
                }

                var decision = await VerifyOneAsync(candidate, cancellationToken);
                if (decision is null) continue;

                decisions.Add(decision);

                if (decision.QueryError == EcpayTradeQueryError.RateLimited)
                {
                    // 被擋了，接下來 30 分鐘問什麼都一樣，剩下的下一輪（之後）再問
                    _logger.LogWarning("付款對帳被綠界擋了（查詢太頻繁），這一輪先停止");
                    break;
                }
            }

            return decisions;
        }

        /// <summary>一筆該問的付款</summary>
        private sealed record Candidate(int PaymentId, string OrderNo, string MerchantTradeNo);

        /// <summary>
        /// 找出該問的付款：付款處理中、最新那張付款單送出超過 FirstQueryAfter、
        /// 而且最近 RequeryInterval 內沒問過。送出最早的先問，最多 MaxQueriesPerRound 筆
        /// </summary>
        private Task<List<Candidate>> FindCandidatesAsync(DateTime now, CancellationToken ct)
        {
            var sentBefore = now - Rules.FirstQueryAfter;
            var lastQueriedBefore = now - Rules.RequeryInterval;

            return (from p in _db.Payments.AsNoTracking()
                    join o in _db.Orders.AsNoTracking() on p.OrderId equals o.OrderId
                    // 最新那張付款單（處理中的就是它）
                    let send = _db.PaymentTransactions
                        .Where(t => t.PaymentId == p.PaymentId
                                 && t.EventType == (byte)PaymentTransactionEventType.Send)
                        .OrderByDescending(t => t.PaymentTransactionId)
                        .FirstOrDefault()
                    // 這筆付款最後一次問綠界的時間；沒問過是 null
                    let lastQueriedAt = _db.PaymentTransactions
                        .Where(t => t.PaymentId == p.PaymentId
                                 && t.EventType == (byte)PaymentTransactionEventType.QueryTradeInfo)
                        .Max(t => (DateTime?)t.CreatedAt)
                    where p.PaymentStatus == (byte)PaymentStatus.Processing
                       && send != null
                       && send.CreatedAt <= sentBefore
                       && (lastQueriedAt == null || lastQueriedAt <= lastQueriedBefore)
                    orderby send.CreatedAt
                    select new Candidate(p.PaymentId, o.OrderNo, send.MerchantTradeNo))
                   .Take(Rules.MaxQueriesPerRound)
                   .ToListAsync(ct);
        }

        /// <returns>處理的結果；這一筆發生例外是 null（已經寫了 log，下一輪再試）</returns>
        private async Task<EcpayPaymentDecision?> VerifyOneAsync(Candidate candidate, CancellationToken ct)
        {
            try
            {
                return await _verifier.VerifyAsync(
                    new EcpayPaymentTarget(candidate.PaymentId, candidate.OrderNo),
                    candidate.MerchantTradeNo,
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站：往上傳，讓背景服務正常結束
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "訂單 {OrderNo} 付款對帳失敗（{MerchantTradeNo}），下一輪再試",
                    candidate.OrderNo, candidate.MerchantTradeNo);

                // 清掉這一筆留在 DbContext 裡、還沒存成功的資料，避免下一筆 SaveChanges 時又一起送出
                _db.ChangeTracker.Clear();
                return null;
            }
        }
    }
}