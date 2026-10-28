namespace VenueGo.Models.PaymentModels
{
    /// <summary>綠界的付款結果從哪一條路送來</summary>
    public enum EcpayNotificationChannel
    {
        /// <summary>綠界伺服器通知（ReturnURL）：要回 1|OK，不回的話綠界會再送</summary>
        ServerNotify,

        /// <summary>瀏覽器帶回（OrderResultURL）：處理完要把客人導回前台</summary>
        BrowserReturn,
    }

    /// <summary>
    /// 綠界送來的付款結果（伺服器通知、瀏覽器帶回的欄位一樣，官方文件 2878）。
    /// 只放會用到的欄位，完整內容存在 PaymentTransactions.RawData
    /// </summary>
    /// <param name="MerchantId">特店編號，要跟我們的設定一樣</param>
    /// <param name="MerchantTradeNo">我們的交易編號，用來找出是哪一筆付款</param>
    /// <param name="RtnCode">1＝付款成功；10300066＝待確認；其他＝失敗；沒有這個欄位是 null</param>
    /// <param name="RtnMsg">綠界的說明文字</param>
    /// <param name="TradeNo">綠界的交易編號</param>
    /// <param name="TradeAmount">綠界說收到多少錢</param>
    /// <param name="PaymentDate">綠界的付款時間（真實時間）</param>
    /// <param name="PaymentType">付款方式，例如 Credit_CreditCard。決定 1：不限信用卡，都接受</param>
    /// <param name="IsSimulated">綠界後台的「模擬付款」：不是真的收到錢，官方文件說要忽略</param>
    public sealed record EcpayPaymentNotification(
        string MerchantId,
        string MerchantTradeNo,
        int? RtnCode,
        string? RtnMsg,
        string? TradeNo,
        int? TradeAmount,
        DateTime? PaymentDate,
        string? PaymentType,
        bool IsSimulated);

    /// <summary>處理綠界付款結果的結論</summary>
    public enum EcpayNotificationOutcome
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

        /// <summary>收到失敗通知，但綠界回答「還沒付款」或問不到：維持處理中，等綠界再送一次</summary>
        ResultUnknown,

        // ── 不處理 ──

        /// <summary>不用處理：模擬付款、舊付款單的失敗通知、付款已經不在處理中</summary>
        Ignored,

        /// <summary>不能相信或對不上：防偽章不對、特店編號不對、找不到付款單、缺少金額</summary>
        Rejected,

        /// <summary>暫時性的問題（被別的操作搶先、建立票券失敗）：整筆已經復原，等綠界再送一次</summary>
        RetryLater,
    }

    /// <summary>處理綠界付款結果的結果</summary>
    /// <param name="Outcome">結論</param>
    /// <param name="OrderNo">訂單編號（VG 開頭），瀏覽器帶回時用來把客人導回這一筆；找不到或不能相信時是 null</param>
    public sealed record EcpayNotificationResult(EcpayNotificationOutcome Outcome, string? OrderNo)
    {
        /// <summary>
        /// 要不要回綠界「1|OK」（只有伺服器通知會用到）。
        /// 原則是「綠界再送一次，結果會不會不一樣？」
        /// 會（還不知道結果、暫時性的錯誤）就不回，讓綠界再送；不會（處理完了、再送也一樣）就回。
        /// 不能相信的通知也不回：那不是綠界送的
        /// </summary>
        public bool ShouldAcknowledge => Outcome is not (
            EcpayNotificationOutcome.ResultUnknown
            or EcpayNotificationOutcome.Rejected
            or EcpayNotificationOutcome.RetryLater);
    }
}