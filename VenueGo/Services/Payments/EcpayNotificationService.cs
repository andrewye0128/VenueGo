using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Options;
using VenueGo.Models.PaymentModels;
using Outcome = VenueGo.Models.PaymentModels.EcpayPaymentOutcome;
using ProcessResult = VenueGo.Enums.PaymentTransactionProcessResult;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 處理綠界送來的付款結果（伺服器通知、瀏覽器帶回共用同一套規則，官方文件 2878）。
    /// <para>
    /// 【這裡負責】找出付款 → 檢查防偽章、特店編號 → 依 RtnCode 分成「成功、待確認、失敗」→ 記錄這則通知
    /// </para>
    /// <para>
    /// 【交給付款確認員（IEcpayPaymentVerifier）】成功：收款、判斷重複付款；
    /// 失敗：先問綠界再決定（3-C-4 決定 2：C）。排程對帳也用同一個付款確認員，規則只有一份
    /// </para>
    /// <para>
    /// 【紀錄】每一則都寫進 PaymentTransactions，只新增、不修改
    /// </para>
    /// </summary>
    public class EcpayNotificationService : IEcpayNotificationService
    {
        private readonly dbVenueContext _db;
        private readonly EcpayOptions _ecpay;
        private readonly IEcpayPaymentVerifier _verifier;
        private readonly IPaymentTransactionRecorder _recorder;
        private readonly ILogger<EcpayNotificationService> _logger;

        public EcpayNotificationService(
            dbVenueContext db,
            IOptionsSnapshot<EcpayOptions> ecpay,
            IEcpayPaymentVerifier verifier,
            IPaymentTransactionRecorder recorder,
            ILogger<EcpayNotificationService> logger)
        {
            _db = db;
            _ecpay = ecpay.Value;
            _verifier = verifier;
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
            var (decision, notificationRecorded) = await DecideAsync(received, cancellationToken);

            // 3. 記錄這則通知（失敗的通知在問綠界之前就先記錄了，見 HandleFailureAsync）
            if (!notificationRecorded)
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

        /// <returns>
        /// Decision：處理的結果；NotificationRecorded：這則通知是不是已經記錄了
        /// （失敗的通知在問綠界之前就先記錄，其他的由 HandleAsync 處理完再記錄）
        /// </returns>
        private async Task<(EcpayPaymentDecision Decision, bool NotificationRecorded)> DecideAsync(
            Received received, CancellationToken ct)
        {
            var notification = received.Notification;

            // 防偽章不對：不是綠界送的，或內容被改過，一個字都不能相信
            if (!received.IsMacValid)
            {
                _logger.LogWarning(
                    "綠界付款結果的防偽章不對：交易編號 {MerchantTradeNo}（{Channel}）",
                    notification.MerchantTradeNo, received.Channel);
                return NotRecordedYet(Outcome.Rejected, ProcessResult.InvalidMac);
            }

            if (notification.MerchantId != _ecpay.MerchantId)
            {
                _logger.LogWarning(
                    "綠界付款結果的特店編號 {MerchantId} 不是我們的：交易編號 {MerchantTradeNo}",
                    notification.MerchantId, notification.MerchantTradeNo);
                return NotRecordedYet(Outcome.Rejected, ProcessResult.LogOnly);
            }

            // 綠界後台的「模擬付款」：沒有真的收到錢
            if (notification.IsSimulated)
            {
                return NotRecordedYet(Outcome.Ignored, ProcessResult.LogOnly);
            }

            return notification.RtnCode switch
            {
                EcpayProtocol.RtnCodes.Success
                    => (await _verifier.ApplyPaidAsync(
                            received.Target, notification.TradeNo, notification.TradeAmount, ct),
                        false),

                // 待確認：不能當成功，也不能當失敗。維持處理中，之後由排程查詢
                EcpayProtocol.RtnCodes.PendingConfirmation
                    => NotRecordedYet(Outcome.PendingConfirmation, ProcessResult.LogOnly),

                _ => (await HandleFailureAsync(received, ct), true),
            };
        }

        /// <summary>
        /// 綠界說沒成功（3-C-4 決定 2：C）：先記下這則通知，再請付款確認員去問綠界。
        /// 先記錄再問，資料表裡的順序就是事情發生的順序；這一列只記錄，處理結果記在查詢那一列
        /// </summary>
        private async Task<EcpayPaymentDecision> HandleFailureAsync(Received received, CancellationToken ct)
        {
            await RecordNotificationAsync(received, ProcessResult.LogOnly, ct);
            return await _verifier.VerifyAsync(received.Target, received.Notification.MerchantTradeNo, ct);
        }

        /// <summary>這裡就判斷完了，通知等 HandleAsync 再記錄</summary>
        private static (EcpayPaymentDecision Decision, bool NotificationRecorded) NotRecordedYet(
            Outcome outcome, ProcessResult processResult)
            => (new EcpayPaymentDecision(outcome, processResult), false);

        // ══ 共用的小工具 ═══════════════════════════════

        /// <summary>用我們的交易編號，找出是哪一筆付款、哪一張訂單</summary>
        private Task<EcpayPaymentTarget?> FindTargetAsync(string merchantTradeNo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(merchantTradeNo))
            {
                return Task.FromResult<EcpayPaymentTarget?>(null);
            }

            return (from t in _db.PaymentTransactions.AsNoTracking()
                    join p in _db.Payments.AsNoTracking() on t.PaymentId equals p.PaymentId
                    join o in _db.Orders.AsNoTracking() on p.OrderId equals o.OrderId
                    where t.MerchantTradeNo == merchantTradeNo
                       && t.EventType == (byte)PaymentTransactionEventType.Send
                    select new EcpayPaymentTarget(p.PaymentId, o.OrderNo))
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

        /// <summary>收到的一則付款結果，加上防偽章的檢查結果</summary>
        private sealed record Received(
            EcpayPaymentTarget Target,
            EcpayPaymentNotification Notification,
            IReadOnlyDictionary<string, string> Fields,
            EcpayNotificationChannel Channel,
            bool IsMacValid);
    }
}