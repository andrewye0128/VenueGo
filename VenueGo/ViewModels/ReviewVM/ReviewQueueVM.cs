namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 館方佇列頁（AReview/Index，三個 tab 共用）
    /// </summary>
    public class ReviewQueueVM
    {
        /// <summary>unread / pending / spam。決定哪個 tab active、查詢用哪組條件。</summary>
        public string Tab { get; init; } = "unread";

        public string? Keyword { get; init; }
        public int? Star { get; init; }
        public bool RatingOnly { get; init; }   // 「僅評分」篩選

        public List<ReviewQueueItemVM> Items { get; init; } = new();

        // ── 各 tab 的未處理數量，顯示在 tab 上的紅色 badge ──
        public int UnreadCount { get; init; }
        public int PendingReplyCount { get; init; }

        public int Page { get; init; } = 1;
        public int TotalPages { get; init; }

        public bool IsEmpty => Items.Count == 0;

        /// <summary>
        /// 罐頭回覆選項。期中先寫死在 C# 常數，畢業再考慮做成資料表。
        /// 一星與五星的目的完全不同：五星是道謝，一星是問出原因。
        /// 每次載入隨機排序，避免員工因慣性總是選同一句。
        /// </summary>
        public List<string> QuickReplies { get; init; } = new();
    }
}
