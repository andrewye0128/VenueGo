namespace VenueGo.Helpers
{
    /// <summary>
    /// 把時間顯示成「3 小時前」這種相對說法。
    ///
    /// 為什麼值得做：員工在清單上掃過去時，真正想知道的是「這則放多久了」，
    /// 而不是「它是幾月幾號幾點」。相對時間讓人不用心算。
    ///
    /// ⚠️ 相對時間是「render 當下算出來、寫死在 HTML 裡」的。
    ///    頁面開著不動一小時，畫面上還是會寫「3 分鐘前」。
    ///    這個清單每做一次操作就會重新載入，所以影響不大；
    ///    真的在意的話要用 JS 定時重算，但那是另一件事。
    ///
    /// ⚠️ 也因為會失去精確度，呼叫端請一併把完整時間放進 title，
    ///    滑鼠移上去看得到正確時間——稽核的時候會需要。
    /// </summary>
    public static class TimeAgo
    {
        /// <summary>超過這個天數就不用相對說法，直接顯示完整時間。</summary>
        public const int RelativeDays = 7;

        public const string FullFormat = "yyyy/M/d HH:mm";

        /// <param name="time">要顯示的時間。</param>
        /// <param name="now">
        /// 比較基準。預設是 DateTime.Now。
        /// 開放這個參數是為了讓同一頁可以共用同一個基準，測試時也能餵固定值。
        /// </param>
        public static string Of(DateTime time, DateTime? now = null)
        {
            DateTime baseline = now ?? DateTime.Now;
            TimeSpan span = baseline - time;

            // 未來時間：機器時鐘沒對準、或資料有問題。
            // 這時候講「-3 分鐘前」只會讓人更困惑，直接給完整時間。
            if (span < TimeSpan.Zero) return time.ToString(FullFormat);

            if (span < TimeSpan.FromMinutes(1)) return "剛剛";
            if (span < TimeSpan.FromHours(1))   return $"{(int)span.TotalMinutes} 分鐘前";
            if (span < TimeSpan.FromDays(1))    return $"{(int)span.TotalHours} 小時前";
            if (span <= TimeSpan.FromDays(RelativeDays)) return $"{(int)span.TotalDays} 天前";

            return time.ToString(FullFormat);
        }

        /// <summary>給 title 用的完整時間。跟 Of() 成對使用。</summary>
        public static string Full(DateTime time) => time.ToString(FullFormat);
    }
}
