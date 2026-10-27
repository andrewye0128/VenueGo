namespace VenueGo.Enums
{
    // PaymentTransactions 的兩個代碼欄位（3-C-0 決定的值，跟初始化腳本的說明一致）。
    // 兩個 enum 只給 PaymentTransactions 用，放在同一個檔案比較好找

    /// <summary>PaymentTransactions.EventType：這一列是哪一種往來</summary>
    public enum PaymentTransactionEventType : byte
    {
        /// <summary>我們送出付款單（3-C-3）</summary>
        Send = 0,

        /// <summary>綠界伺服器通知 ReturnURL（3-C-4）</summary>
        ReturnUrl = 1,

        /// <summary>瀏覽器帶回 OrderResultURL（3-C-4）</summary>
        OrderResultUrl = 2,

        /// <summary>主動查詢 QueryTradeInfo（3-C-5）</summary>
        QueryTradeInfo = 3,
    }

    /// <summary>PaymentTransactions.ProcessResult：這一列處理的結果</summary>
    public enum PaymentTransactionProcessResult : byte
    {
        /// <summary>只記錄，沒有改付款狀態（送出付款都是這個）</summary>
        LogOnly = 0,
        Confirmed = 1,
        AlreadyProcessed = 2,
        PaymentFailed = 3,
        InvalidMac = 4,
        AmountMismatch = 5,
        ReoccupiedAfterExpiry = 6,
        RefundingAfterExpiry = 7,
    }
}