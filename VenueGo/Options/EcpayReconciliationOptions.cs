namespace VenueGo.Options
{
    /// <summary>
    /// 付款對帳排程的設定（3-C-5），對應設定檔的 Ecpay:Reconciliation。
    /// 都有預設值，不用改 appsettings.json；要調整就在 appsettings.Local.json 加上：
    ///
    ///   "Ecpay": {
    ///     "Reconciliation": { "AbandonAfterMinutes": 30 }
    ///   }
    ///
    /// 時間都是跟 PaymentTransactions.CreatedAt（網站時間）比，所以會跟著時光機走
    /// </summary>
    public sealed class EcpayReconciliationOptions
    {
        /// <summary>付款單送出多久之後才開始問。客人可能還在刷卡；官方文件也建議付款 10 分鐘後再查</summary>
        public int FirstQueryAfterMinutes { get; set; } = 10;

        /// <summary>同一筆付款問過之後，隔多久才再問。綠界問太快會擋 30 分鐘</summary>
        public int RequeryIntervalMinutes { get; set; } = 10;

        /// <summary>
        /// 綠界回答「還沒付款」，而且付款單送出超過這麼久，就當成客人沒刷卡就離開，改回未付款。
        /// 太短的話，銀行還在處理的付款會被誤判（官方文件：信用卡可能 10 分鐘後還是 0）
        /// </summary>
        public int AbandonAfterMinutes { get; set; } = 20;

        /// <summary>每一輪最多問幾筆：卡住的付款累積很多時，不會一口氣問太多被綠界擋</summary>
        public int MaxQueriesPerRound { get; set; } = 20;

        /// <summary>同一輪裡，每問一筆之間間隔多久（毫秒）</summary>
        public int DelayBetweenQueriesMilliseconds { get; set; } = 1000;

        public TimeSpan FirstQueryAfter => TimeSpan.FromMinutes(FirstQueryAfterMinutes);
        public TimeSpan RequeryInterval => TimeSpan.FromMinutes(RequeryIntervalMinutes);
        public TimeSpan AbandonAfter => TimeSpan.FromMinutes(AbandonAfterMinutes);
        public TimeSpan DelayBetweenQueries => TimeSpan.FromMilliseconds(DelayBetweenQueriesMilliseconds);
    }
}