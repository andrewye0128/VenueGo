using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.PaymentModels;
using Outcome = VenueGo.Models.PaymentModels.EcpayPaymentOutcome;
using ProcessResult = VenueGo.Enums.PaymentTransactionProcessResult;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 付款確認員：把「綠界怎麼說」變成「付款要改成什麼」。
    /// <para>
    /// 【已付款】交給 PaymentConfirmationService（後台標記已付款也用它），再判斷是不是重複付款
    /// </para>
    /// <para>
    /// 【問綠界】只問「付款處理中、而且是最新那張」的付款單。確定失敗才改成付款失敗，
    /// 綠界說其實付了就照收款處理；不知道結果就不動，絕對不能當成失敗，否則客人可能被扣兩次錢
    /// </para>
    /// <para>
    /// 收到通知（3-C-4）、排程對帳、處理中又按付款（3-C-5）都用這裡，規則只有一份
    /// </para>
    /// </summary>
    public class EcpayPaymentVerifier : IEcpayPaymentVerifier
    {
        private readonly dbVenueContext _db;
        private readonly IPaymentConfirmationService _confirmation;
        private readonly IEcpayTradeQueryService _tradeQuery;
        private readonly IPaymentTransactionRecorder _recorder;
        private readonly ILogger<EcpayPaymentVerifier> _logger;

        public EcpayPaymentVerifier(
            dbVenueContext db,
            IPaymentConfirmationService confirmation,
            IEcpayTradeQueryService tradeQuery,
            IPaymentTransactionRecorder recorder,
            ILogger<EcpayPaymentVerifier> logger)
        {
            _db = db;
            _confirmation = confirmation;
            _tradeQuery = tradeQuery;
            _recorder = recorder;
            _logger = logger;
        }

        // ══ 綠界說付款成功 ═══════════════════════════════

        public async Task<EcpayPaymentDecision> ApplyPaidAsync(
            EcpayPaymentTarget target, string? tradeNo, int? amount, CancellationToken cancellationToken = default)
        {
            // 一定要有金額：沒有金額的話，收款確認會跳過金額檢查（那是給後台標記已付款用的）
            if (amount is null || string.IsNullOrEmpty(tradeNo))
            {
                _logger.LogError(
                    "綠界付款成功的資料缺少金額或綠界交易編號：訂單 {OrderNo}", target.OrderNo);
                return new EcpayPaymentDecision(Outcome.Rejected, ProcessResult.LogOnly);
            }

            var result = await _confirmation.ConfirmPaidAsync(
                new PaymentConfirmRequest(
                    target.PaymentId,
                    PaymentConfirmSource.Ecpay,
                    OperatorUserId: null,
                    PaidAmount: amount,
                    TransactionNo: tradeNo),
                cancellationToken);

            return result.Outcome switch
            {
                PaymentConfirmOutcome.Confirmed
                    => new EcpayPaymentDecision(Outcome.Confirmed, ProcessResult.Confirmed),
                PaymentConfirmOutcome.ReoccupiedAfterExpiry
                    => new EcpayPaymentDecision(Outcome.ReoccupiedAfterExpiry, ProcessResult.ReoccupiedAfterExpiry),
                PaymentConfirmOutcome.RefundingAfterExpiry
                    => new EcpayPaymentDecision(Outcome.RefundingAfterExpiry, ProcessResult.RefundingAfterExpiry),
                PaymentConfirmOutcome.AlreadyProcessed
                    => await CheckDuplicateAsync(target, tradeNo, amount.Value, cancellationToken),

                // 收款確認已經寫了 log；再送幾次金額也一樣，所以算「處理完了」
                PaymentConfirmOutcome.AmountMismatch
                    => new EcpayPaymentDecision(Outcome.AmountMismatch, ProcessResult.AmountMismatch),

                // 被搶先、建立票券失敗：整筆已經復原，等下一次再處理
                _ => RetryLater(target, result),
            };
        }

        /// <summary>
        /// 這筆錢已經記錄過了。是同一筆綠界交易重送（正常），還是客人付了第二次（要退款）？
        /// 用綠界的交易編號判斷：Payments.TransactionNo 記的是第一次收款的那一筆
        /// </summary>
        private async Task<EcpayPaymentDecision> CheckDuplicateAsync(
            EcpayPaymentTarget target, string tradeNo, int amount, CancellationToken ct)
        {
            var recordedTradeNo = await _db.Payments.AsNoTracking()
                .Where(p => p.PaymentId == target.PaymentId)
                .Select(p => p.TransactionNo)
                .FirstOrDefaultAsync(ct);

            if (recordedTradeNo == tradeNo)
            {
                return new EcpayPaymentDecision(Outcome.AlreadyProcessed, ProcessResult.AlreadyProcessed);
            }

            // 用 Warning：這筆錢要有人去退，log 裡要看得到（同 PaymentConfirmationService 的退款處理中）
            _logger.LogWarning(
                "訂單 {OrderNo} 重複付款：已記錄綠界交易 {RecordedTradeNo}，又收到 {TradeNo} 付款成功 {Amount} 元，請到櫃檯退款",
                target.OrderNo, recordedTradeNo, tradeNo, amount);
            return new EcpayPaymentDecision(Outcome.DuplicatePayment, ProcessResult.DuplicatePayment);
        }

        private EcpayPaymentDecision RetryLater(EcpayPaymentTarget target, PaymentConfirmResult result)
        {
            _logger.LogWarning(
                "訂單 {OrderNo} 收款確認沒有完成（{Outcome}：{Message}），等下一次再處理",
                target.OrderNo, result.Outcome, result.Message);
            return new EcpayPaymentDecision(Outcome.RetryLater, ProcessResult.LogOnly);
        }

        // ══ 問綠界 ═══════════════════════════════

        public async Task<EcpayPaymentDecision> VerifyAsync(
            EcpayPaymentTarget target, string merchantTradeNo, CancellationToken cancellationToken = default)
        {
            // 只處理「還在處理中、而且是最新那張」的付款單：
            // 客人可能失敗後又重新付款了，舊付款單的結果不能影響新的付款單
            if (!await AwaitingResult(target.PaymentId, merchantTradeNo).AnyAsync(cancellationToken))
            {
                return new EcpayPaymentDecision(Outcome.Ignored, ProcessResult.LogOnly);
            }

            var query = await _tradeQuery.QueryAsync(merchantTradeNo, cancellationToken);
            var decision = await ApplyQueryResultAsync(target, merchantTradeNo, query, cancellationToken);

            // 判斷的依據是綠界的回答，所以處理結果記在查詢這一列
            await _recorder.RecordQueryAsync(
                target.PaymentId, merchantTradeNo, query, decision.ProcessResult, cancellationToken);
            return decision;
        }

        /// <summary>依綠界的回答處理。之後要加新的規則（例如「還沒付款太久就改回未付款」），加在這裡</summary>
        private async Task<EcpayPaymentDecision> ApplyQueryResultAsync(
            EcpayPaymentTarget target, string merchantTradeNo, EcpayTradeQueryResult query, CancellationToken ct)
        {
            return query.Status switch
            {
                // 綠界說其實付款成功了：照收款處理（金額、綠界交易編號用查詢的回答）
                EcpayTradeQueryStatus.Paid
                    => await ApplyPaidAsync(target, query.Trade!.TradeNo, query.Trade.TradeAmount, ct),

                EcpayTradeQueryStatus.Failed
                    => await MarkFailedAsync(target, merchantTradeNo, ct),

                // 還沒付款（銀行可能還沒回覆）、問不到：結果不明，維持處理中
                _ => new EcpayPaymentDecision(
                    Outcome.ResultUnknown,
                    query.Error == EcpayTradeQueryError.InvalidMac ? ProcessResult.InvalidMac : ProcessResult.LogOnly),
            };
        }

        /// <summary>
        /// 付款改成失敗。條件跟 VerifyAsync 一開始的檢查一樣（AwaitingResult）：
        /// 問綠界的這段時間，如果狀態被別的操作改了（例如另一則通知剛好收款），就改不到
        /// </summary>
        private async Task<EcpayPaymentDecision> MarkFailedAsync(
            EcpayPaymentTarget target, string merchantTradeNo, CancellationToken ct)
        {
            var updated = await AwaitingResult(target.PaymentId, merchantTradeNo)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PaymentStatus, (byte)PaymentStatus.Failed), ct);

            if (updated == 0)
            {
                return new EcpayPaymentDecision(Outcome.Ignored, ProcessResult.LogOnly);
            }

            _logger.LogInformation(
                "訂單 {OrderNo} 綠界確認交易失敗（{MerchantTradeNo}），付款改成付款失敗，可以重新付款",
                target.OrderNo, merchantTradeNo);
            return new EcpayPaymentDecision(Outcome.MarkedFailed, ProcessResult.PaymentFailed);
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
    }
}