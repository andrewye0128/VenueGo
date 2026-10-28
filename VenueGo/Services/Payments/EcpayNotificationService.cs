using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.Options;
using VenueGo.Models.PaymentModels;
using Outcome = VenueGo.Models.PaymentModels.EcpayNotificationOutcome;
using ProcessResult = VenueGo.Enums.PaymentTransactionProcessResult;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 處理綠界送來的付款結果（伺服器通知、瀏覽器帶回共用同一套規則，官方文件 2878）。
    /// <para>
    /// 【順序】找出付款 → 檢查防偽章、特店編號 → 依 RtnCode 分成「成功、待確認、失敗」
    /// </para>
    /// <para>
    /// 【成功】交給 PaymentConfirmationService（後台標記已付款也用它）。這裡只多判斷「重複付款」
    /// </para>
    /// <para>
    /// 【失敗】先問綠界（3-C-4 決定 2：C），確定失敗才改成付款失敗；綠界說其實付了，就照收款處理；
    /// 還沒結果或問不到，就維持處理中。不知道結果時絕對不能當成失敗，否則客人可能被扣兩次錢
    /// </para>
    /// <para>
    /// 【紀錄】每一則都寫進 PaymentTransactions，只新增、不修改
    /// </para>
    /// </summary>
    public class EcpayNotificationService : IEcpayNotificationService
    {
        private readonly dbVenueContext _db;
        private readonly EcpayOptions _ecpay;
        private readonly IPaymentConfirmationService _confirmation;
        private readonly IEcpayTradeQueryService _tradeQuery;
        private readonly IPaymentTransactionRecorder _recorder;
        private readonly ILogger<EcpayNotificationService> _logger;

        public EcpayNotificationService(
            dbVenueContext db,
            IOptionsSnapshot<EcpayOptions> ecpay,
            IPaymentConfirmationService confirmation,
            IEcpayTradeQueryService tradeQuery,
            IPaymentTransactionRecorder recorder,
            ILogger<EcpayNotificationService> logger)
        {
            _db = db;
            _ecpay = ecpay.Value;
            _confirmation = confirmation;
            _tradeQuery = tradeQuery;
            _recorder = recorder;
            _logger = logger;
        }

        public async Task<EcpayNotificationResult> HandleAsync(
            IReadOnlyDictionary<string, string> fields,
            EcpayNotificationChannel channel,
            CancellationToken cancellationToken = default)
        {
            var notification = Parse(fields);

            // 1. 找出是哪一筆付款：用送出付款單時記下的交易編號
            var target = await FindTargetAsync(notification.MerchantTradeNo, cancellationToken);
            if (target is null)
            {
                // 找不到就沒有 PaymentId，不能寫進 PaymentTransactions，只寫 log
                _logger.LogWarning(
                    "收到綠界付款結果（{Channel}），但找不到交易編號 {MerchantTradeNo}",
                    channel, notification.MerchantTradeNo);
                return new EcpayNotificationResult(Outcome.Rejected, OrderNo: null);
            }

            var received = new Received(
                target, notification, fields, channel,
                IsMacValid: EcpayCheckMac.Verify(fields, _ecpay.HashKey, _ecpay.HashIV));

            // 2. 判斷並處理
            var decision = await DecideAsync(received, cancellationToken);

            // 3. 記錄這則通知（失敗的通知在問綠界之前就先記錄了，見 HandleFailureAsync）
            if (!decision.NotificationRecorded)
            {
                await RecordNotificationAsync(received, decision.ProcessResult, cancellationToken);
            }

            _logger.LogInformation(
                "綠界付款結果（{Channel}）：訂單 {OrderNo}，交易編號 {MerchantTradeNo}，RtnCode {RtnCode} → {Outcome}",
                channel, target.OrderNo, notification.MerchantTradeNo, notification.RtnCode, decision.Outcome);

            // 不能相信的通知，不回傳是哪一筆訂單
            var orderNo = decision.Outcome == Outcome.Rejected ? null : target.OrderNo;
            return new EcpayNotificationResult(decision.Outcome, orderNo);
        }

        // ══ 判斷 ═══════════════════════════════

        private async Task<Decision> DecideAsync(Received received, CancellationToken ct)
        {
            var notification = received.Notification;

            // 防偽章不對：不是綠界送的，或內容被改過，一個字都不能相信
            if (!received.IsMacValid)
            {
                _logger.LogWarning(
                    "綠界付款結果的防偽章不對：交易編號 {MerchantTradeNo}（{Channel}）",
                    notification.MerchantTradeNo, received.Channel);
                return new Decision(Outcome.Rejected, ProcessResult.InvalidMac);
            }

            if (notification.MerchantId != _ecpay.MerchantId)
            {
                _logger.LogWarning(
                    "綠界付款結果的特店編號 {MerchantId} 不是我們的：交易編號 {MerchantTradeNo}",
                    notification.MerchantId, notification.MerchantTradeNo);
                return new Decision(Outcome.Rejected, ProcessResult.LogOnly);
            }

            // 綠界後台的「模擬付款」：沒有真的收到錢
            if (notification.IsSimulated)
            {
                return new Decision(Outcome.Ignored, ProcessResult.LogOnly);
            }

            return notification.RtnCode switch
            {
                EcpayProtocol.RtnCodes.Success
                    => await ConfirmAsync(received.Target, notification.TradeNo, notification.TradeAmount, ct),

                // 待確認：不能當成功，也不能當失敗。維持處理中，之後由排程查詢（3-C-5）
                EcpayProtocol.RtnCodes.PendingConfirmation
                    => new Decision(Outcome.PendingConfirmation, ProcessResult.LogOnly),

                _ => await HandleFailureAsync(received, ct),
            };
        }

        // ══ 收到錢 ═══════════════════════════════

        /// <summary>
        /// 交給收款確認服務：改成已付款、預約確認、建立票券，逾期後才收到錢的情況它也會處理。
        /// 這裡只多做一件事：判斷是不是重複付款
        /// </summary>
        private async Task<Decision> ConfirmAsync(Target target, string? tradeNo, int? amount, CancellationToken ct)
        {
            // 一定要有金額：沒有金額的話，收款確認會跳過金額檢查（那是給後台標記已付款用的）
            if (amount is null || string.IsNullOrEmpty(tradeNo))
            {
                _logger.LogError(
                    "綠界付款成功的資料缺少金額或綠界交易編號：訂單 {OrderNo}", target.OrderNo);
                return new Decision(Outcome.Rejected, ProcessResult.LogOnly);
            }

            var result = await _confirmation.ConfirmPaidAsync(
                new PaymentConfirmRequest(
                    target.PaymentId,
                    PaymentConfirmSource.Ecpay,
                    OperatorUserId: null,
                    PaidAmount: amount,
                    TransactionNo: tradeNo),
                ct);

            return result.Outcome switch
            {
                PaymentConfirmOutcome.Confirmed
                    => new Decision(Outcome.Confirmed, ProcessResult.Confirmed),
                PaymentConfirmOutcome.ReoccupiedAfterExpiry
                    => new Decision(Outcome.ReoccupiedAfterExpiry, ProcessResult.ReoccupiedAfterExpiry),
                PaymentConfirmOutcome.RefundingAfterExpiry
                    => new Decision(Outcome.RefundingAfterExpiry, ProcessResult.RefundingAfterExpiry),
                PaymentConfirmOutcome.AlreadyProcessed
                    => await CheckDuplicateAsync(target, tradeNo, amount.Value, ct),

                // 收款確認已經寫了 log；再送幾次金額也一樣，所以算「處理完了」
                PaymentConfirmOutcome.AmountMismatch
                    => new Decision(Outcome.AmountMismatch, ProcessResult.AmountMismatch),

                // 被搶先、建立票券失敗：整筆已經復原，等綠界再送一次
                _ => RetryLater(target, result),
            };
        }

        /// <summary>
        /// 這筆錢已經記錄過了。是同一筆綠界交易重送（正常），還是客人付了第二次（要退款）？
        /// 用綠界的交易編號判斷：Payments.TransactionNo 記的是第一次收款的那一筆
        /// </summary>
        private async Task<Decision> CheckDuplicateAsync(Target target, string tradeNo, int amount, CancellationToken ct)
        {
            var recordedTradeNo = await _db.Payments.AsNoTracking()
                .Where(p => p.PaymentId == target.PaymentId)
                .Select(p => p.TransactionNo)
                .FirstOrDefaultAsync(ct);

            if (recordedTradeNo == tradeNo)
            {
                return new Decision(Outcome.AlreadyProcessed, ProcessResult.AlreadyProcessed);
            }

            // 用 Warning：這筆錢要有人去退，log 裡要看得到（同 PaymentConfirmationService 的退款處理中）
            _logger.LogWarning(
                "訂單 {OrderNo} 重複付款：已記錄綠界交易 {RecordedTradeNo}，又收到 {TradeNo} 付款成功 {Amount} 元，請到櫃檯退款",
                target.OrderNo, recordedTradeNo, tradeNo, amount);
            return new Decision(Outcome.DuplicatePayment, ProcessResult.DuplicatePayment);
        }

        private Decision RetryLater(Target target, PaymentConfirmResult result)
        {
            _logger.LogWarning(
                "訂單 {OrderNo} 收款確認沒有完成（{Outcome}：{Message}），等綠界再送一次",
                target.OrderNo, result.Outcome, result.Message);
            return new Decision(Outcome.RetryLater, ProcessResult.LogOnly);
        }

        // ══ 綠界說沒成功 ═══════════════════════════════

        /// <summary>決定 2（C）：先問綠界確認，確定失敗才改；不確定就維持處理中</summary>
        private async Task<Decision> HandleFailureAsync(Received received, CancellationToken ct)
        {
            var target = received.Target;
            var merchantTradeNo = received.Notification.MerchantTradeNo;

            // 只處理「還在處理中、而且是最新那張」的付款單：
            // 客人可能失敗後又重新付款了，舊付款單的失敗通知不能影響新的付款單
            if (!await AwaitingResult(target.PaymentId, merchantTradeNo).AnyAsync(ct))
            {
                return new Decision(Outcome.Ignored, ProcessResult.LogOnly);
            }

            // 先記下這則通知，再問綠界：資料表裡的順序就是事情發生的順序。
            // 這一列只記錄，判斷的依據是綠界的回答，結果記在查詢那一列
            await RecordNotificationAsync(received, ProcessResult.LogOnly, ct);

            var query = await _tradeQuery.QueryAsync(merchantTradeNo, ct);

            var decision = query.Status switch
            {
                // 綠界說其實付款成功了：照收款處理（金額、綠界交易編號用查詢的回答）
                EcpayTradeQueryStatus.Paid
                    => await ConfirmAsync(target, query.Trade!.TradeNo, query.Trade.TradeAmount, ct),

                EcpayTradeQueryStatus.Failed
                    => await MarkFailedAsync(target, merchantTradeNo, ct),

                // 還沒付款（銀行可能還沒回覆）、問不到：結果不明，維持處理中
                _ => new Decision(
                    Outcome.ResultUnknown,
                    query.Error == EcpayTradeQueryError.InvalidMac ? ProcessResult.InvalidMac : ProcessResult.LogOnly),
            };

            await _recorder.RecordQueryAsync(target.PaymentId, merchantTradeNo, query, decision.ProcessResult, ct);
            return decision with { NotificationRecorded = true };
        }

        /// <summary>
        /// 付款改成失敗。條件跟 HandleFailureAsync 一開始的檢查一樣（AwaitingResult）：
        /// 問綠界的這段時間，如果狀態被別的操作改了（例如另一則通知剛好收款），就改不到
        /// </summary>
        private async Task<Decision> MarkFailedAsync(Target target, string merchantTradeNo, CancellationToken ct)
        {
            var updated = await AwaitingResult(target.PaymentId, merchantTradeNo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PaymentStatus, (byte)PaymentStatus.Failed), ct);

            if (updated == 0)
            {
                return new Decision(Outcome.Ignored, ProcessResult.LogOnly);
            }

            _logger.LogInformation(
                "訂單 {OrderNo} 綠界確認交易失敗（{MerchantTradeNo}），付款改成付款失敗，可以重新付款",
                target.OrderNo, merchantTradeNo);
            return new Decision(Outcome.MarkedFailed, ProcessResult.PaymentFailed);
        }

        /// <summary>
        /// 「還在等結果」的付款：付款處理中，而且這張付款單是最新的那一張。
        /// 檢查（AnyAsync）和修改（ExecuteUpdateAsync）共用這個條件，兩邊不會不一致
        /// </summary>
        private IQueryable<Payment> AwaitingResult(int paymentId, string merchantTradeNo)
        {
            return _db.Payments.Where(p =>
                p.PaymentId == paymentId
                && p.PaymentStatus == (byte)PaymentStatus.Processing
                && _db.PaymentTransactions
                    .Where(t => t.PaymentId == paymentId
                             && t.EventType == (byte)PaymentTransactionEventType.Send)
                    .OrderByDescending(t => t.PaymentTransactionId)
                    .Select(t => t.MerchantTradeNo)
                    .FirstOrDefault() == merchantTradeNo);
        }

        // ══ 共用的小工具 ═══════════════════════════════

        /// <summary>用我們的交易編號，找出是哪一筆付款、哪一張訂單</summary>
        private Task<Target?> FindTargetAsync(string merchantTradeNo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(merchantTradeNo))
            {
                return Task.FromResult<Target?>(null);
            }

            return (from t in _db.PaymentTransactions.AsNoTracking()
                    join p in _db.Payments.AsNoTracking() on t.PaymentId equals p.PaymentId
                    join o in _db.Orders.AsNoTracking() on p.OrderId equals o.OrderId
                    where t.MerchantTradeNo == merchantTradeNo
                       && t.EventType == (byte)PaymentTransactionEventType.Send
                    select new Target(p.PaymentId, o.OrderNo))
                   .FirstOrDefaultAsync(ct);
        }

        private Task RecordNotificationAsync(Received received, ProcessResult processResult, CancellationToken ct)
            => _recorder.RecordNotificationAsync(
                received.Target.PaymentId,
                received.Channel,
                received.Notification,
                received.IsMacValid,
                received.Fields,
                processResult,
                ct);

        /// <summary>綠界的欄位 → EcpayPaymentNotification。空字串當成沒有值</summary>
        private static EcpayPaymentNotification Parse(IReadOnlyDictionary<string, string> fields)
        {
            string? Get(string name) => fields.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

            return new EcpayPaymentNotification(
                MerchantId: Get("MerchantID") ?? string.Empty,
                MerchantTradeNo: Get("MerchantTradeNo") ?? string.Empty,
                RtnCode: EcpayProtocol.ParseInt(Get("RtnCode")),
                RtnMsg: Get("RtnMsg"),
                TradeNo: Get("TradeNo"),
                TradeAmount: EcpayProtocol.ParseInt(Get("TradeAmt")),
                PaymentDate: EcpayProtocol.ParseDateTime(Get("PaymentDate")),
                PaymentType: Get("PaymentType"),
                IsSimulated: Get("SimulatePaid") == "1");
        }

        /// <summary>處理需要的資料（只讀）</summary>
        private sealed record Target(int PaymentId, string OrderNo);

        /// <summary>收到的一則付款結果，加上防偽章的檢查結果</summary>
        private sealed record Received(
            Target Target,
            EcpayPaymentNotification Notification,
            IReadOnlyDictionary<string, string> Fields,
            EcpayNotificationChannel Channel,
            bool IsMacValid);

        /// <summary>判斷的結果</summary>
        /// <param name="Outcome">結論</param>
        /// <param name="ProcessResult">這則通知要記成什麼</param>
        /// <param name="NotificationRecorded">這則通知是不是已經記錄了（失敗的通知在問綠界之前就先記錄）</param>
        private sealed record Decision(Outcome Outcome, ProcessResult ProcessResult, bool NotificationRecorded = false);
    }
}