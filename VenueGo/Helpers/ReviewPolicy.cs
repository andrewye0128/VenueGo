namespace VenueGo.Helpers
{
    /// <summary>
    /// 評論子系統的業務規則常數。
    /// ⚠️ 團隊若已經有放常數的地方（例如 CDictionary），整份搬過去即可，
    ///    引用端只要改 using。
    /// </summary>
    public static class ReviewPolicy
    {
        /// <summary>評論建立後幾天，沒回覆也會自動公開。</summary>
        public const int PublicBufferDays = 7;

        /// <summary>建立後幾天開始顯示橘色「快到期」提醒。</summary>
        public const int OverdueWarnDays = 3;

        /// <summary>館方回覆的長度上限。對應 ReviewMain.ReplyContent 的 nvarchar(1000)。</summary>
        public const int ReplyMaxLength = 1000;

        /// <summary>垃圾標記理由。陣列索引就是存進資料庫的 SpamReason 值（0～7）。</summary>
        public static readonly string[] SpamReasons =
        {
            "謾罵",        // 0
            "猥褻",        // 1
            "威脅",        // 2
            "亂碼",        // 3
            "個資",        // 4
            "廣告",        // 5
            "無關或不實",  // 6
            "仇恨歧視"     // 7
        };

        public static string SpamReasonText(byte? reason) =>
            reason is byte b && b < SpamReasons.Length ? SpamReasons[b] : "未知理由";

        /// <summary>
        /// 罐頭回覆。Label 是 chip 上的短字，Text 是點了之後插入輸入框的內容。
        /// 期中先寫死，之後要讓館方自己維護再搬進資料表。
        /// </summary>
        public static readonly CannedReply[] CannedReplies =
        {
            new("感謝評價",   "感謝您撥空留下評價，我們會持續維持服務品質，期待您再次光臨。"),
            new("已轉交處理", "感謝您的反映，我們已將這個問題轉交相關人員處理，造成不便深感抱歉。"),
            new("會檢查設施", "感謝您的提醒，我們會盡快檢查相關設施，處理完成前可能仍有不便，敬請見諒。"),
            new("歡迎再來",   "很高興您這次的體驗愉快，歡迎再來運動！"),
            new("請聯繫櫃檯", "若方便，歡迎到服務櫃檯或來電告知更多細節，讓我們能更完整地協助您。")
        };
    }

    public sealed record CannedReply(string Label, string Text);
}
