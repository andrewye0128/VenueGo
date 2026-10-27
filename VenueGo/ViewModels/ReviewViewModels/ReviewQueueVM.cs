using System.Text.RegularExpressions;
using VenueGo.Models.ReviewModels;

namespace VenueGo.ViewModels.ReviewViewModels
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
    /// 10/27：已完成清單依顧客對館方回覆的表態篩選（Reviews.ReplySatisfaction：0 不滿意／1 普通／2 滿意／null 未表態）。
    /// 只有已完成清單用得到；其他清單一律當作 All。
    /// </summary>
    public static class QueueSatisfaction
    {
        public const string All         = "all";
        public const string Unsatisfied = "unsatisfied";
        public const string Neutral     = "neutral";
        public const string Satisfied   = "satisfied";
        public const string None        = "none";
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

    /// <summary>
    /// 未回覆的時間警示。
    /// 10/26 負評優先：Soon、Late 依星等不同（規則在 Models/ReviewModels/ReviewUrgency_Claude.cs）。原本：
    ///     None,       // 不用提醒
    ///     Soon,       // 橘：滿 3 天
    ///     Overdue     // 紅：已滿 7 天
    /// </summary>
    public enum OverdueLevel
    {
        None,       // 不用提醒
        Soon,       // 橘：期限當天、已過目標期限，或放了滿 3 天
        Late,       // 紅：還沒閱覽，超過閱覽的最遲期限（10/26 新增）
        Overdue     // 紅：已滿 7 天（已經自動公開）
    }

    /// <summary>10/26：一則評論現在該做什麼。</summary>
    public enum DeadlineStage
    {
        None,       // 已回覆或已標垃圾，不用做
        Read,       // 還沒閱覽
        Reply       // 閱覽過，還沒回覆
    }

    /// <summary>分組的依據。9/29 加：原本只有依訂單，現在多了依場地。</summary>
    public enum QueueGroupKind
    {
        Order,
        Venue
    }

    /// <summary>
    /// 一組評論。兩種分組共用同一個類別，畫面的外框、置頂浮上來的規則都一樣。
    ///
    /// 依訂單：一張訂單可能有好幾則評論——EntryTicket 是依訂單人數建立的，
    ///         N 個人就有 N 次現場評論的機會，再加上一則預約評論。
    /// 依場地：選了運動類型時自動改用這個，把同一個場地的問題放在一起看（9/29）。
    /// </summary>
    public sealed record ReviewQueueGroupVM
    {
        public QueueGroupKind Kind { get; init; } = QueueGroupKind.Order;

        /// <summary>依訂單時用。null 代表這則評論追不到訂單（資料異常），單獨顯示、不畫框。</summary>
        public int? OrderId { get; init; }
        public string? OrderNo { get; init; }

        /// <summary>依場地時用。null 代表追不到場地（資料異常）。</summary>
        public int? VenueId { get; init; }
        public string? VenueName { get; init; }

        public List<ReviewQueueItemVM> Items { get; init; } = new();

        /// <summary>組內只要有一則被置頂，整組就浮到最上面。</summary>
        public bool HasPinned => Items.Any(i => i.IsPinned);

        // ── 10/23 分頁：同一組可能被切到兩頁 ──
        /// <summary>這組在整份清單裡共有幾則（Items 只有這一頁的）。沒分頁時是 0，看 Items.Count。</summary>
        public int TotalCount { get; init; }
        /// <summary>這組前面還有評論在上一頁（組的標題標「接上一頁」）。</summary>
        public bool ContinuedFromPrevious { get; init; }
        /// <summary>這組後面還有評論在下一頁（組的標題標「下一頁還有」）。</summary>
        public bool ContinuesOnNext { get; init; }
        /// <summary>整組的則數：分頁時看 TotalCount，沒分頁時看 Items.Count。</summary>
        public int AllCount => Math.Max(TotalCount, Items.Count);

        /// <summary>
        /// 要不要畫框。
        ///   依訂單：只有一則的時候不畫框——框起來是為了表達「這些是一起的」。
        ///   依場地：只有一則也畫框——框的標題就是場地名稱，「這個場地只有 1 則」本身就是資訊，
        ///           而且不畫的話，那一則會看起來像沒被分組。
        /// </summary>
        public bool IsRealGroup => Kind switch
        {
            // 10/23：分頁後這一頁可能只剩這組的其中一則，所以看整組的則數（AllCount），不看這一頁的
            QueueGroupKind.Order => OrderId != null && AllCount > 1,
            QueueGroupKind.Venue => VenueId != null,
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "未定義的分組依據")
        };
    }

    /// <summary>運動類型下拉選單的一個選項。</summary>
    public sealed record SportTypeOption(int SportTypeId, string Text);

    /// <summary>
    /// 某一頁第一則的「排序依據時間」與是否置頂（10/23，評論管理長按快轉時顯示）。
    /// Time：已完成是回覆時間、垃圾桶是標記時間，其餘是評論時間（跟清單的排序一致）。
    /// </summary>
    /// <remarks>10/26：未讀、待回覆改成依期限排序，Time 改放期限，Prefix 放「期限 」讓畫面上看得懂。原本只有 Time、IsPinned。</remarks>
    public sealed record QueuePageMark(DateTime Time, bool IsPinned, string Prefix = "");

    /// <summary>館方評論清單的整頁資料。Index 與 QueueList 共用同一份。</summary>
    public sealed record ReviewQueueVM
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

        /// <summary>
        /// 依訂單分組。10/27 起預設開啟（原本預設關閉）：同一筆訂單的評論者多半互相認識，反映的問題也常重疊，放在一起處理比較方便。
        /// （9/29 舊說明，v2 已不成立）⚠️ 有選運動類型時一律是 false（改成依場地分組），開關也會停用。
        /// v2：這裡存的是「使用者有沒有打開開關」，選了運動類型或場地時也照樣記著，
        ///     條件拿掉後就恢復分組。畫面上實際有沒有依訂單分組，看 GroupByOrder。
        /// </summary>
        public bool Grouped { get; init; } = true;   // 10/27：預設開啟。原本：public bool Grouped { get; init; }

        // ── 運動類型與場地（9/29）──

        /// <summary>選了哪個運動類型。null＝全部。只篩得到現場評論（預約評論沒有場地資料）。</summary>
        public int? SportTypeId { get; init; }

        /// <summary>
        /// 只看某個場地。null＝不限。
        /// （9/29 舊說明）有值的時候，SportTypeId 一定是這個場地的運動類型。
        /// v2：有值的時候，SportTypeId 是 null（全部）或這個場地的運動類型。
        /// </summary>
        public int? VenueId { get; init; }

        /// <summary>目前篩選的場地名稱，給「正在篩選『某場地』的評論」那行字用。</summary>
        public string? VenueFilterName { get; init; }

        public List<SportTypeOption> SportTypeOptions { get; init; } = new();

        /// <summary>選了運動類型或場地就依場地分組，取代依訂單分組。</summary>
        // 原本：public bool GroupByVenue => SportTypeId != null;
        public bool GroupByVenue => SportTypeId != null || VenueId != null;

        /// <summary>
        /// 實際上有沒有依訂單分組（v2）：使用者打開了，而且沒有被依場地分組擋住。
        /// 開關的勾選狀態看這個；網址與 data-grouped 仍然用 Grouped，才記得住使用者的選擇。
        /// </summary>
        public bool GroupByOrder => Grouped && !GroupByVenue;

        /// <summary>沒分組時看這個。</summary>
        public List<ReviewQueueItemVM> Items { get; init; } = new();

        /// <summary>分組時看這個。Items 仍然是完整清單，只是排列方式不同。</summary>
        public List<ReviewQueueGroupVM> Groups { get; init; } = new();

        // 分頁標籤上的數字。會跟著來源、時間範圍、搜尋一起變——
        // badge 上的數字如果跟眼前看到的清單對不起來，員工會以為系統壞了。
        public int UnreadCount  { get; init; }
        public int PendingCount { get; init; }
        public int SpamCount    { get; init; }

        // ── 10/23 分頁（每頁幾則在 AReviewController.PageSize）──
        /// <summary>目前是第幾頁（後端調整過，一定在 1～TotalPages 之間）。</summary>
        public int Page { get; init; } = 1;
        /// <summary>總頁數，至少 1。</summary>
        public int TotalPages { get; init; } = 1;
        /// <summary>每一頁第一則的排序時間，第 n 頁是 PageMarks[n - 1]。</summary>
        public List<QueuePageMark> PageMarks { get; init; } = new();
        /// <summary>符合條件的總則數（Items 只有這一頁）。</summary>
        public int TotalItemCount { get; init; }

        /// <summary>
        /// 10/26：全部、已完成清單在預設條件下，沒有列出的「只有星等」評論有幾則（跟顧客端評論專區一樣）。
        /// 0 就不顯示說明。
        /// </summary>
        public int RatingOnlyHidden { get; init; }

        /// <summary>
        /// 10/26：未讀、待回覆清單最後那一段「只有星等」的評論有幾則（不含置頂的）。
        /// 分隔線上顯示「只有星等（N 則）」。依訂單、場地分組時不畫分隔線，這個是 0。
        /// </summary>
        public int RatingOnlySectionCount { get; init; }

        /// <summary>10/27：已完成清單的滿意度篩選（QueueSatisfaction）。其他清單一律是 All。</summary>
        public string Satisfaction { get; init; } = QueueSatisfaction.All;

        /// <summary>
        /// 10/27：已完成清單裡，各種表態各有幾則（鍵是 QueueSatisfaction 的值，不含 All）。
        /// 套用來源、時間範圍、場地、搜尋，但不套用滿意度本身，數字才會跟按鈕對得上。其他清單是空的。
        /// </summary>
        public Dictionary<string, int> SatisfactionCounts { get; init; } = new();

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
            || !Grouped   // 10/27：預設改成依訂單分組，關掉才算改了條件。原本：|| Grouped
            || SportTypeId != null
            || VenueId != null
            || Satisfaction != QueueSatisfaction.All;   // 10/27
    }
}
