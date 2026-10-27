namespace VenueGo.Models.PaymentModels
{
    /// <summary>是誰通知「收到錢了」</summary>
    public enum PaymentConfirmSource
    {
        /// <summary>後台員工按「標記已付款」（現場付款）</summary>
        Staff,

        /// <summary>綠界（伺服器通知、瀏覽器帶回、主動查詢、排程查詢）</summary>
        Ecpay,
    }

    /// <summary>收款確認的輸入</summary>
    /// <param name="PaymentId">哪一筆付款</param>
    /// <param name="Source">誰通知的</param>
    /// <param name="OperatorUserId">後台員工的 UserId；綠界是 null（寫進 AuditLogs.UserId）</param>
    /// <param name="PaidAmount">綠界說收到多少錢，要跟訂單金額一樣；後台是 null（不檢查）</param>
    /// <param name="TransactionNo">綠界的交易編號 TradeNo，存進 Payments.TransactionNo；後台是 null</param>
    public sealed record PaymentConfirmRequest(
        int PaymentId,
        PaymentConfirmSource Source,
        int? OperatorUserId = null,
        int? PaidAmount = null,
        string? TransactionNo = null);

    /// <summary>收款確認的結果種類</summary>
    public enum PaymentConfirmOutcome
    {
        /// <summary>一般收款：預約改成已確認、建立票券</summary>
        Confirmed,

        /// <summary>這筆已經處理過了（已付款、退款處理中、已退款），這次什麼都沒改</summary>
        AlreadyProcessed,

        /// <summary>逾期後才收到錢，時段還空著：重新佔位、恢復成已確認</summary>
        ReoccupiedAfterExpiry,

        /// <summary>預約已經失效（逾期後時段被訂走、時段已開始、預約已取消），付款改成退款處理中</summary>
        RefundingAfterExpiry,

        /// <summary>綠界說的金額跟訂單金額不一樣，什麼都沒改</summary>
        AmountMismatch,

        /// <summary>找不到這筆付款</summary>
        NotFound,

        /// <summary>後台：預約已結束或已取消，不能標記已付款</summary>
        NotPayable,

        /// <summary>建立票券失敗，整筆復原</summary>
        TicketFailed,

        /// <summary>重來一次還是被別的操作搶先，整筆放棄</summary>
        Conflict,
    }

    public sealed record PaymentConfirmResult(
        PaymentConfirmOutcome Outcome,
        string Message,
        string? OrderNo = null,
        int TicketCount = 0)
    {
        /// <summary>
        /// 這筆錢有沒有記錄下來（不管是收款、退款處理中，還是之前就處理過）。
        /// 3-C-4 用它決定要不要回應綠界「1|OK」：沒記錄下來就不回，綠界會再通知一次
        /// </summary>
        public bool IsMoneyRecorded => Outcome is PaymentConfirmOutcome.Confirmed
            or PaymentConfirmOutcome.AlreadyProcessed
            or PaymentConfirmOutcome.ReoccupiedAfterExpiry
            or PaymentConfirmOutcome.RefundingAfterExpiry;
    }
}