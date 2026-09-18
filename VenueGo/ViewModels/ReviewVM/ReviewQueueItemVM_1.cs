namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 館方佇列的一列（未讀／待回覆／垃圾桶共用）
    /// 摘要與完整內容都裝在同一個物件裡，因為 accordion 展開時
    /// 不換頁、不再發請求，完整內容必須一開始就送到前端。
    /// </summary>
    public class ReviewQueueItemVM_1
    {
        public int ReviewId { get; init; }
        public byte StarRating { get; init; }
        public DateTime CreatedAt { get; init; }
        public string DisplayName { get; init; } = "";
        public string? VenueName { get; init; }

        // ── 摘要列顯示用 ──
        public string? Content { get; init; }

        /// <summary>清單摘要用的前 30 字。在 ViewModel 算好，View 只負責印。</summary>
        public string Summary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content))
                    return "（僅評分，未留言）";

                return Content.Length <= 30
                         ? Content
                         : Content.Substring(0, 30) + "…";
            }
        }

        /// <summary>是否為純評分評論。用來做「僅評分」篩選標籤與罐頭回覆的啟用條件。</summary>
        public bool IsRatingOnly => string.IsNullOrWhiteSpace(Content);

        // ── 處理狀態 ──
        public DateTime? ReadAt { get; init; }
        public string? ReadByEmployeeName { get; init; }   // 顯示「由 ○○ 於 ○/○ 閱覽」
        public bool IsPinned { get; init; }

        public DateTime? RepliedAt { get; init; }
        public string? ReplyContent { get; init; }

        public DateTime? SpamMarkedAt { get; init; }
        public byte? SpamReason { get; init; }
        public string? SpamMarkedByEmployeeName { get; init; }

        public bool IsRead => ReadAt != null;
        public bool IsReplied => RepliedAt != null;
        public bool IsSpam => SpamMarkedAt != null;
    }
}
