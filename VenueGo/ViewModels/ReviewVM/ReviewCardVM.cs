using System.ComponentModel;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 公開評論卡片（顧客端評論專區用）
    /// 刻意不包含 SpamReason、ReadByEmployeeId 等館方內部欄位，
    /// 從結構上杜絕洩漏。
    /// </summary>
    public class ReviewCardViewModel
    {
        public int ReviewId { get; init; }
        [DisplayName("評分")]
        public byte StarRating { get; init; }
        [DisplayName("評論內容")]
        public string? Content { get; init; }
        [DisplayName("評論時間")]
        public DateTime CreatedAt { get; init; }

        public bool MentionsVenue { get; init; }
        public bool MentionsStaff { get; init; }

        /// <summary>場地名稱。預約類評論為 null，版面要能處理它不存在。</summary>
        [DisplayName("租借場地名稱")]
        public string? VenueName { get; init; }

        /// <summary>顯示名稱。匿名時用隨機暱稱，實名時用會員姓名。</summary>
        [DisplayName("評論署名")]
        public string DisplayName { get; init; } = "";

        // ── 館方回覆（沒有回覆時三者皆 null）──
        [DisplayName("館方回覆內容")]
        public string? ReplyContent { get; init; }
        [DisplayName("評論時間")]
        public DateTime? RepliedAt { get; init; }
        [DisplayName("是否已回覆")]
        public bool HasReply => RepliedAt != null;

        /// <summary>
        /// 顯示名稱的判定邏輯集中在這裡，View 就不必寫 if。
        /// 匿名但暱稱意外是 null 時要有 fallback，否則畫面會空白。
        /// </summary>
        public static string ResolveDisplayName(
            bool isAnonymous, string? anonymousNickname, string? memberName)
            => isAnonymous
                ? (anonymousNickname ?? "匿名使用者")
                : (memberName ?? "使用者");

    }
}
