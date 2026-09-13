namespace VenueGo.ViewModels.ReviewVM
{
    public class MyReviewPageVM
    {
        // 需要的欄位：
        //   ReviewId          ← 表單 POST 時要帶（切換公開、表態滿意度）
        //   QrToken           ← 現場評論的通行憑據，操作完要轉址回本頁
        //   StarRating / ReviewContent / CreatedAt
        //   DisplayName       ← 匿名顯示暱稱、實名顯示會員姓名
        //   VenueName / RentStartTime   ← 預約評論兩者皆 null
        //   MentionsVenue / MentionsStaff
        //   IsPublic          ← 本人可切換，要顯示當前狀態
        //   IsSpamMarked      ← 被標記垃圾時公開切換要隱藏
        //   ReplyContent / RepliedAt
        //   ReplyViewedAt     ← 決定要不要顯示「新回覆」標記
        //   ReplySatisfaction ← 已表態顯示結果，未表態顯示三顆按鈕
        public int ReviewId { get; init; }

        public string Qrtoken { get; init; } = null!;

        public byte StarRating { get; init; }

        public string? ReviewContent { get; init; }

        public bool IsAnonymous { get; init; }

        public bool IsPublic { get; set; }

        public bool MentionsVenue { get; init; }

        public bool MentionsStaff { get; init; }

        public DateTime CreatedAt { get; init; }

        public string DisplayName { get; init; }

        public string? ReplyContent { get; init; }

        public DateTime? RepliedAt { get; init; }

        public DateTime? ReplyViewedAt { get; set; }

        public byte? ReplySatisfaction { get; set; }

        public bool IsSpamMarked { get; set; }

        // 建議加的計算屬性：
        //   HasReply            => RepliedAt != null
        //   CanToggleVisibility => !IsSpamMarked
        //   HasUnviewedReply    => RepliedAt != null && ReplyViewedAt == null
        //   CanRateSatisfaction => HasReply && ReplySatisfaction == null
        //
        // ⚠️ 進入本頁就代表「顧客看到回覆了」，MarkReplyViewed 要在
        //    這裡記錄 ReplyViewedAt——但只在 HasReply 且尚未記錄時，
        //    否則每次重新整理都會蓋掉原本的時間。
        public bool HasReply => RepliedAt != null;
        public bool CanToggleVisibility => !IsSpamMarked;
        public bool HasUnviewedReply => RepliedAt != null && ReplyViewedAt == null;
        public bool CanRateSatisfaction => HasReply && ReplySatisfaction == null;

    }
}
