using VenueGo.Enums;

namespace VenueGo.Models.PaymentModels
{
    /// <summary>要處理的付款：哪一筆付款，以及它的訂單編號（寫 log 用）</summary>
    /// <param name="PaymentId">Payments.PaymentId</param>
    /// <param name="OrderNo">訂單編號（VG 開頭）</param>
    public sealed record EcpayPaymentTarget(int PaymentId, string OrderNo);

    /// <summary>處理的結果：結論，以及要記進 PaymentTransactions 的處理結果</summary>
    /// <param name="Outcome">結論</param>
    /// <param name="ProcessResult">記進 PaymentTransactions.ProcessResult 的值</param>
    public sealed record EcpayPaymentDecision(EcpayPaymentOutcome Outcome, PaymentTransactionProcessResult ProcessResult);

    /// <summary>處理綠界付款結果的結論（收到通知、排程對帳、開始付款前確認共用）</summary>
    public enum EcpayPaymentOutcome
    {
        // ── 收到錢（交給 PaymentConfirmationService）──

        /// <summary>收款成功：預約已確認、票券已建立</summary>
        Confirmed,

        /// <summary>這筆已經處理過了：同一則通知又送一次，或兩條路都送到</summary>
        AlreadyProcessed,

        /// <summary>逾期後才收到錢，時段還空著，已重新佔位</summary>
        ReoccupiedAfterExpiry,

        /// <summary>逾期後才收到錢，預約已失效，付款改成退款處理中</summary>
        RefundingAfterExpiry,

        /// <summary>⚠️ 已經付過錢，又收到另一筆綠界交易的付款成功：客人被扣兩次，要退款</summary>
        DuplicatePayment,

        /// <summary>綠界說的金額跟訂單不一樣，狀態沒有改</summary>
        AmountMismatch,

        // ── 沒收到錢 ──

        /// <summary>問過綠界，確定交易失敗：付款改成失敗，客人可以重新付款</summary>
        MarkedFailed,

        /// <summary>綠界說「待確認」（10300066）：維持處理中，之後由排程查詢</summary>
        PendingConfirmation,

        /// <summary>問綠界的結果是「還沒付款」或問不到：維持處理中，之後再問</summary>
        ResultUnknown,

        // ── 不處理 ──

        /// <summary>不用處理：模擬付款、舊付款單的失敗通知、付款已經不在處理中</summary>
        Ignored,

        /// <summary>不能相信或對不上：防偽章不對、特店編號不對、找不到付款單、缺少金額</summary>
        Rejected,

        /// <summary>暫時性的問題（被別的操作搶先、建立票券失敗）：整筆已經復原，等下一次再處理</summary>
        RetryLater,
    }
}
