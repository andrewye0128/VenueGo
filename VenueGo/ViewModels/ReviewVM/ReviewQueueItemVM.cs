namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>館方清單裡的一則評論。顯示用，全部 init。</summary>
    public class ReviewQueueItemVM
    {
        public int ReviewId { get; init; }
        public byte StarRating { get; init; }
        public DateTime CreatedAt { get; init; }
        public required string DisplayName { get; init; }
        public bool IsAnonymous { get; init; }
        public bool IsPublic { get; init; }
        public string? Content { get; init; }
        public bool MentionsVenue { get; init; }
        public bool MentionsStaff { get; init; }

        // ── 來源：現場評論有場地與時段，預約評論有訂單編號 ──
        public bool IsBookingReview { get; init; }
        public string? VenueName { get; init; }
        public DateTime? RentStartTime { get; init; }
        public string? OrderNo { get; init; }

        // ── 處理狀態 ──
        public DateTime? ReadAt { get; init; }
        public string? ReadByEmployeeName { get; init; }
        public bool IsPinned { get; init; }

        public DateTime? SpamMarkedAt { get; init; }
        public string? SpamReasonText { get; init; }
        public string? SpamMarkedByEmployeeName { get; init; }

        /// <summary>
        /// 由 Controller 用「同一個現在時間」算好再放進來。
        /// 不寫成計算屬性，是因為那樣每讀一次就取一次 DateTime.Now，
        /// 同一頁上每則評論的判斷基準會不一樣。
        /// </summary>
        public OverdueLevel OverdueLevel { get; init; }

        // ── 計算屬性 ──
        public bool IsRead => ReadAt != null;
        public bool IsRatingOnly => string.IsNullOrWhiteSpace(Content);

        /// <summary>
        /// 已滿 3 天、沒回覆、顧客選了公開 → 依規則已經出現在評論專區。
        /// 預約評論的 IsPublic 一律是 false，所以不必另外排除。
        /// </summary>
        public bool IsAutoPublished => OverdueLevel == OverdueLevel.Overdue && IsPublic;

        /// <summary>摘要列的前 30 字。</summary>
        public string Summary
        {
            get
            {
                if (IsRatingOnly) return "（僅評分，未留言）";
                return Content!.Length <= 30 ? Content : Content[..30] + "…";
            }
        }
    }
}
