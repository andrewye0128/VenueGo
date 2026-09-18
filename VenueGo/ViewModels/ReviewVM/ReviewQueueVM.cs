using VenueGo.Helpers;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>三個清單的名字。查詢字串、連結、比對都用這裡的常數，不要手打字串。</summary>
    public static class QueueTab
    {
        public const string Unread  = "unread";
        public const string Pending = "pending";
        public const string Spam    = "spam";
    }

    /// <summary>評論來源篩選。</summary>
    public static class QueueSource
    {
        public const string All     = "all";
        public const string Visit   = "visit";
        public const string Booking = "booking";
    }

    /// <summary>未回覆的時間警示。</summary>
    public enum OverdueLevel
    {
        None,       // 不用提醒
        Soon,       // 橘：快滿 3 天
        Overdue     // 紅：已滿 3 天
    }

    /// <summary>館方評論清單的整頁資料。Index 與 QueueList 共用同一份。</summary>
    public class ReviewQueueVM
    {
        public string Tab    { get; init; } = QueueTab.Unread;
        public string Source { get; init; } = QueueSource.All;

        public List<ReviewQueueItemVM> Items { get; init; } = new();

        // 分頁標籤上的數字（會跟著來源篩選變）
        public int UnreadCount  { get; init; }
        public int PendingCount { get; init; }
        public int SpamCount    { get; init; }

        /// <summary>已經打亂順序的罐頭回覆，整頁共用一組。</summary>
        public List<CannedReply> CannedReplies { get; init; } = new();

        public bool IsEmpty => Items.Count == 0;
    }
}
