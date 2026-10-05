namespace VenueGo.Models.ReviewModels
{
    /// <summary>
    /// 評論子系統的業務規則常數。
    /// ⚠️ 團隊若已經有放常數的地方（例如 CDictionary），整份搬過去即可，
    ///    引用端只要改 using。
    /// </summary>
    public static class ReviewPolicy
    {
        /// <summary>評論資格（憑證）的有效天數，從憑證建立起算。</summary>
        public const int TicketValidDays = 14;

        /// <summary>評論建立後幾天，沒回覆也會自動公開。</summary>
        public const int PublicBufferDays = 7;

        /// <summary>建立後幾天開始顯示橘色「快到期」提醒。</summary>
        public const int OverdueWarnDays = 3;

        /// <summary>館方回覆的長度上限。對應 ReviewMain.ReplyContent 的 nvarchar(1000)。</summary>
        public const int ReplyMaxLength = 1000;

        /// <summary>垃圾標記理由。陣列索引就是存進資料庫的 SpamReason 值（0～8）。</summary>
        public static readonly SpamReasonInfo[] SpamReasonInfos =
        {
            new("謾罵",           "辱罵、髒話、人身攻擊（對服務的強烈負評不算）",                "評論中含有辱罵或人身攻擊的字詞"),
            new("猥褻",           "露骨的性內容",                                                "評論中含有露骨的性內容"),
            new("威脅",           "揚言傷害他人、破壞設施或報復",                                "評論中含有威脅他人或設施安全的內容"),
            new("亂碼",           "沒有可讀內容，如亂打鍵盤、重複同一個字",                      "評論沒有可以閱讀的內容"),
            new("個資",           "規則抓不到的個資，如姓名、地址（電話、Email 會自動遮蔽）",    "評論中含有可以辨識個人身分的資料"),
            new("廣告",           "推銷商品、服務、其他場館或社群",                              "評論中含有推銷或廣告內容"),
            new("無關或不實",     "與本館的體驗無關，或經查證不實",                              "評論內容與本館的使用體驗無關，或與事實不符"),
            new("仇恨歧視",       "針對族群、性別、性傾向、身心障礙、國籍的貶低",                "評論中含有貶低特定族群的內容"),
            new("騷擾或不當言論", "針對員工或其他顧客的外貌、身體、性暗示、私生活（對服務或設施的負評不算）", "評論中含有針對個人外貌、身體或私生活的不當言論"),
        };

        /// <summary>
        /// 既有程式都用這個，保留不動：內容由上面自動產生。
        /// SpamReasonInfos 一定要放在 SpamReasons 上面。
        /// </summary>
        public static readonly string[] SpamReasons = SpamReasonInfos.Select(x => x.Label).ToArray();

        //public static readonly string[] SpamReasons =
        //{
        //    "謾罵",           // 0：辱罵、髒話、人身攻擊（對服務的強烈負評不算）
        //    "猥褻",           // 1：露骨的性內容
        //    "威脅",           // 2：揚言傷害他人、破壞設施或報復
        //    "亂碼",           // 3：沒有可讀內容，如亂打鍵盤、重複同一個字
        //    "個資",           // 4：規則抓不到的個資，如姓名、地址（電話、Email 會自動遮蔽）
        //    "廣告",           // 5：推銷商品、服務、其他場館或社群
        //    "無關或不實",     // 6：與本館的體驗無關，或經查證不實
        //    "仇恨歧視",       // 7：針對族群、性別、性傾向、身心障礙、國籍的貶低
        //    "騷擾或不當言論"  // 8：針對員工或其他顧客的外貌、身體、性暗示、私生活（對服務或設施的負評不算）
        //};

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

    public sealed record SpamReasonInfo(string Label, string StaffHint, string CustomerText);
    public sealed record CannedReply(string Label, string Text);
}
