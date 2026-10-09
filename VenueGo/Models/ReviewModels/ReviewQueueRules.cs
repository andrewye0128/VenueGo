using System.Linq.Expressions;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Models.ReviewModels
{
    /// <summary>
    /// 館方評論審核的規則：一則評論屬於哪個清單、能不能做某個操作、做了之後欄位怎麼變。
    /// <para>
    /// 【為何從 AReviewController 搬出來】
    /// 原本是控制器裡的 private static 方法，測試專案看不到，沒辦法寫單元測試。
    /// 這些規則都不碰資料庫、不碰 HttpContext，只看 ReviewMain 的欄位，
    /// 放在獨立的類別裡就能直接測，第 6 步（館方清單整合）改規則時也比較安心。
    /// </para>
    /// <para>
    /// 控制器用 <c>using static</c> 引入這個類別，所以呼叫的地方不用改名字。
    /// </para>
    /// </summary>
    public static class ReviewQueueRules
    {
        // ════════════════════════════════════════════════════════
        //  清單條件
        // ════════════════════════════════════════════════════════

        // ── 三個清單的條件，各只寫一次 ──
        //
        //  Expression<Func<...>> 就是「還沒執行的 Where 條件」。
        //  寫成欄位之後，查清單和算數量可以共用同一個條件，
        //  不會出現「清單改了、數量忘了改」的情況。

        /// <summary>未讀：還沒有員工展開過。</summary>
        public static readonly Expression<Func<ReviewMain, bool>> UnreadRule =
            r => r.ReadAt == null;

        /// <summary>待回覆：讀過了，但還沒回覆、也沒標成垃圾。</summary>
        public static readonly Expression<Func<ReviewMain, bool>> PendingRule =
            r => r.ReadAt != null && r.RepliedAt == null && r.SpamMarkedAt == null;

        /// <summary>垃圾：被員工標記過。</summary>
        public static readonly Expression<Func<ReviewMain, bool>> SpamRule =
            r => r.SpamMarkedAt != null;

        /// <summary>
        /// 把 PendingRule 編譯成一般的方法，給 CanHandle 用。
        /// <para>
        /// 原本 CanHandle 是另外手寫一份同樣的條件，旁邊註明「必須和 PendingRule 一致」。
        /// 直接從 PendingRule 編譯出來，兩邊就不可能不一致。
        /// 編譯有成本，所以只在類別載入時做一次。
        /// </para>
        /// </summary>
        private static readonly Func<ReviewMain, bool> IsPending = PendingRule.Compile();

        // ════════════════════════════════════════════════════════
        //  單筆操作的前置條件（已經從資料庫拿出來的物件用這兩個）
        // ════════════════════════════════════════════════════════

        /// <summary>還沒讀過才能標記已讀。</summary>
        public static bool CanMarkRead(ReviewMain r) => r.ReadAt == null;

        /// <summary>在「待回覆」清單裡的才能回覆、標記垃圾、置頂。</summary>
        public static bool CanHandle(ReviewMain r) => IsPending(r);

        // ════════════════════════════════════════════════════════
        //  查詢字串的值不可信任，不認得的一律改回預設
        // ════════════════════════════════════════════════════════

        public static string NormalizeTab(string? tab) => tab switch
        {
            QueueTab.All       => QueueTab.All,
            QueueTab.Pending   => QueueTab.Pending,
            QueueTab.Completed => QueueTab.Completed,
            QueueTab.Spam      => QueueTab.Spam,
            _                  => QueueTab.Unread
        };

        public static string NormalizeSource(string? source) => source switch
        {
            QueueSource.Visit   => QueueSource.Visit,
            QueueSource.Booking => QueueSource.Booking,
            _                   => QueueSource.All
        };

        public static string NormalizeRange(string? range) => range switch
        {
            QueueRange.Today    => QueueRange.Today,
            QueueRange.Week     => QueueRange.Week,
            QueueRange.Quarter  => QueueRange.Quarter,
            QueueRange.HalfYear => QueueRange.HalfYear,
            QueueRange.All      => QueueRange.All,
            _                   => QueueRange.Default   // 不認得就回預設（一個月內）
        };

        public static string NormalizeSearchField(string? field) => field switch
        {
            QueueSearchField.OrderNo  => QueueSearchField.OrderNo,
            QueueSearchField.Employee => QueueSearchField.Employee,
            _                         => QueueSearchField.Default
        };

        /// <summary>
        /// 把使用者打的字拆成多個關鍵字，空格等於「而且」。
        /// 輸入「網球 破掉」→ 找同時含有「網球」和「破掉」的評論。
        ///
        /// ⚠️ 全形空格也要當分隔。中文輸入法很容易打出全形空格，
        ///    使用者看起來跟半形一樣，但字元不同，不處理的話會變成
        ///    去搜「網球　破掉」這一整串，當然搜不到。
        /// </summary>
        public static List<string> SplitKeywords(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return new List<string>();

            char[] separators = { ' ', '　', '\t' };

            return keyword.Split(separators,
                                 StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Distinct()
                          .Take(QueueSearchField.MaxKeywords)
                          .ToList();
        }

        // ════════════════════════════════════════════════════════
        //  逾期提醒
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 還沒處理的評論放了多久。已回覆或已標垃圾的不提醒。
        /// <para>剛好滿 OverdueWarnDays 天就算「快逾期」，剛好滿 PublicBufferDays 天就算「已逾期」。</para>
        /// </summary>
        public static OverdueLevel GetOverdueLevel(ReviewMain r, DateTime now)
        {
            if (r.RepliedAt != null || r.SpamMarkedAt != null) return OverdueLevel.None;

            var age = now - r.CreatedAt;
            if (age >= TimeSpan.FromDays(ReviewPolicy.PublicBufferDays)) return OverdueLevel.Overdue;
            if (age >= TimeSpan.FromDays(ReviewPolicy.OverdueWarnDays))  return OverdueLevel.Soon;
            return OverdueLevel.None;
        }

        // ════════════════════════════════════════════════════════
        //  寫入：只改實體的值，SaveChanges 由 Action 呼叫
        // ════════════════════════════════════════════════════════

        public static void ApplyRead(ReviewMain r, int employeeId, DateTime now)
        {
            r.ReadAt = now;
            r.ReadByEmployeeId = employeeId;
        }

        public static void ApplyReply(ReviewMain r, string content, int employeeId, DateTime now)
        {
            r.ReplyContent = content;
            r.RepliedAt = now;
            r.RepliedByEmployeeId = employeeId;
            r.IsPinned = false;     // 回覆後就離開待回覆清單，置頂沒有意義了
        }

        public static void ApplySpam(ReviewMain r, byte reason, int employeeId, DateTime now)
        {
            r.SpamMarkedAt = now;
            r.SpamMarkedByEmployeeId = employeeId;
            r.SpamReason = reason;
            r.IsPublic = false;     // 規則：垃圾強制不公開
            r.IsPinned = false;
        }
    }
}
