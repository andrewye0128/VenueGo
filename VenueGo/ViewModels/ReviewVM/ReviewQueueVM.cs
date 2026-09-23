using System.Text.RegularExpressions;
using VenueGo.Helpers;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>三個清單的名字。查詢字串、連結、比對都用這裡的常數，不要手打字串。</summary>
    public static class QueueTab
    {
        public const string All = "all";
        public const string Unread  = "unread";
        public const string Pending = "pending";
        public const string Completed = "completed";
        public const string Spam    = "spam";
    }

    /// <summary>評論來源篩選。</summary>
    public static class QueueSource
    {
        public const string All     = "all";
        public const string Visit   = "visit";
        public const string Booking = "booking";
    }

    /// <summary>
    /// 時間範圍篩選。
    ///
    /// 比的是哪一個欄位由清單決定：
    ///   已完成 → RepliedAt（員工關心的是「什麼時候處理的」）
    ///   其他   → CreatedAt（關心的是「顧客什麼時候寫的」）
    ///
    /// Days 是「往前推幾天」，null 代表不篩選。
    /// 全部以「今天 00:00」為基準往前算，不是以「此刻」往前算——
    /// 否則同一天下午跟晚上查會得到不同結果，員工會覺得清單在跳。
    /// </summary>
    public static class QueueRange
    {
        public const string Today    = "today";
        public const string Week     = "week";
        public const string Month    = "month";
        public const string Quarter  = "quarter";
        public const string HalfYear = "half";
        public const string All      = "all";

        /// <summary>預設與「重置」都回到這裡。</summary>
        public const string Default = Month;

        /// <summary>往前推幾天。null = 不篩選。</summary>
        public static int? DaysOf(string range) => range switch
        {
            Today    => 0,
            Week     => 7,
            Month    => 30,
            Quarter  => 90,
            HalfYear => 180,
            _        => null
        };

        /// <summary>下拉選單的文字，順序就是顯示順序。</summary>
        public static readonly (string Value, string Text)[] Options =
        {
            (Today,    "今天"),
            (Week,     "一週內"),
            (Month,    "一個月內"),
            (Quarter,  "三個月內"),
            (HalfYear, "半年內"),
            (All,      "全部")
        };
    }

    /// <summary>搜尋要比對哪個欄位。</summary>
    public static class QueueSearchField
    {
        /// <summary>評論內容 + 館方回覆，兩邊都搜。</summary>
        public const string Content  = "content";
        /// <summary>訂單編號。現場評論走 EntryTicket 接到訂單，所以兩種評論都搜得到。</summary>
        public const string OrderNo  = "orderno";
        /// <summary>處理人員姓名：閱覽者、回覆者、垃圾標記者。</summary>
        public const string Employee = "employee";

        public const string Default = Content;

        public static readonly (string Value, string Text)[] Options =
        {
            (Content,  "評論或回覆內容"),
            (OrderNo,  "訂單編號"),
            (Employee, "處理人員姓名")
        };

        /// <summary>
        /// 一次最多吃幾個關鍵字。
        /// 空格拆出來的每個字都會多一個 Where（也就是多一個 LIKE），
        /// 有人整段話貼進來的話會組出幾十個條件，所以設上限。
        /// </summary>
        public const int MaxKeywords = 5;
    }

    /// <summary>未回覆的時間警示。</summary>
    public enum OverdueLevel
    {
        None,       // 不用提醒
        Soon,       // 橘：滿 3 天
        Overdue     // 紅：已滿 7 天
    }

    /// <summary>
    /// 同一筆訂單的評論包成一組。只有「依訂單分組」開啟時才會用到。
    ///
    /// 一張訂單可能有好幾則評論：EntryTicket 是依訂單人數建立的，
    /// N 個人就有 N 次現場評論的機會，再加上一則預約評論。
    /// </summary>
    public class ReviewQueueGroupVM
    {
        /// <summary>null 代表這則評論追不到訂單（資料異常），單獨顯示、不畫框。</summary>
        public int? OrderId { get; init; }
        public string? OrderNo { get; init; }

        public List<ReviewQueueItemVM> Items { get; init; } = new();

        /// <summary>組內只要有一則被置頂，整組就浮到最上面。</summary>
        public bool HasPinned => Items.Any(i => i.IsPinned);

        /// <summary>只有一則的時候不畫框：框起來是為了表達「這些是一起的」。</summary>
        public bool IsRealGroup => OrderId != null && Items.Count > 1;
    }

    /// <summary>館方評論清單的整頁資料。Index 與 QueueList 共用同一份。</summary>
    public class ReviewQueueVM
    {
        public string Tab    { get; init; } = QueueTab.Unread;
        public string Source { get; init; } = QueueSource.All;

        // ── 時間範圍與搜尋 ──
        public string Range       { get; init; } = QueueRange.Default;
        public string SearchField { get; init; } = QueueSearchField.Default;

        /// <summary>使用者原本打的字，原樣送回填回搜尋框。</summary>
        public string Keyword { get; init; } = "";

        /// <summary>
        /// 拆好、去重、截到上限之後的關鍵字。
        /// 前端拿它來標註命中的字——讓後端決定，前端才不會跟後端拆得不一樣。
        /// </summary>
        public List<string> Keywords { get; init; } = new();

        /// <summary>依訂單分組。預設關閉。</summary>
        public bool Grouped { get; init; }

        /// <summary>沒分組時看這個。</summary>
        public List<ReviewQueueItemVM> Items { get; init; } = new();

        /// <summary>分組時看這個。Items 仍然是完整清單，只是排列方式不同。</summary>
        public List<ReviewQueueGroupVM> Groups { get; init; } = new();

        // 分頁標籤上的數字。會跟著來源、時間範圍、搜尋一起變——
        // badge 上的數字如果跟眼前看到的清單對不起來，員工會以為系統壞了。
        public int UnreadCount  { get; init; }
        public int PendingCount { get; init; }
        public int SpamCount    { get; init; }

        /// <summary>已經打亂順序的罐頭回覆，整頁共用一組。</summary>
        public List<CannedReply> CannedReplies { get; init; } = new();

        /// <summary>
        /// AI 回覆草稿功能是否可用（＝後端有沒有設定金鑰）。
        ///
        /// 預設 false，所以在 BuildQueueVm 設定它之前，按鈕不會出現。
        /// 這是刻意的：組員 clone 下來沒有金鑰也要能正常跑，
        /// 不能因為少一個設定就看到一顆按了必定失敗的按鈕。
        ///
        /// 接上 IReplyDraftService 之後，在 BuildQueueVm 的 return 裡加一行：
        ///     AiDraftEnabled = _replyDraft.IsEnabled,
        /// 想先看 UI 長什麼樣的話，暫時寫死 true 也可以。
        /// </summary>
        public bool AiDraftEnabled { get; init; }

        public bool IsEmpty => Items.Count == 0;

        /// <summary>有沒有套用任何非預設的條件。用來決定「重置」按鈕要不要亮起來。</summary>
        public bool HasFilter =>
            Tab != QueueTab.Unread
            || Source != QueueSource.All
            || Range != QueueRange.Default
            || SearchField != QueueSearchField.Default
            || !string.IsNullOrEmpty(Keyword)
            || Grouped;
    }
}
