using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.ViewModels;
using VenueGo.ViewModels.ReviewVM;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

namespace VenueGo.Controllers
{
    [Authorize]
    public class AReviewController(dbVenueContext db, ICurrentUser currentUser) : Controller
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUser _currentUser = currentUser;

        // ════════════════════════════════════════════════════════
        //  第一區：規則判定
        //  這一區只回答「這則評論現在屬於哪個清單、能不能做某個操作」。
        // ════════════════════════════════════════════════════════

        // ── 三個清單的條件，各只寫一次 ──
        //
        //  Expression<Func<...>> 就是「還沒執行的 Where 條件」。
        //  寫成欄位之後，查清單和算數量可以共用同一個條件，
        //  不會出現「清單改了、數量忘了改」的情況。
        private static readonly Expression<Func<ReviewMain, bool>> UnreadRule =
            r => r.ReadAt == null;

        private static readonly Expression<Func<ReviewMain, bool>> PendingRule =
            r => r.ReadAt != null && r.RepliedAt == null && r.SpamMarkedAt == null;

        private static readonly Expression<Func<ReviewMain, bool>> SpamRule =
            r => r.SpamMarkedAt != null;

        // ── 單筆操作的前置條件（已經從資料庫拿出來的物件用這兩個）──
        //  ⚠️ CanHandle 的內容必須和上面的 PendingRule 一致。
        private static bool CanMarkRead(ReviewMain r) => r.ReadAt == null;

        private static bool CanHandle(ReviewMain r) =>
            r.ReadAt != null && r.RepliedAt == null && r.SpamMarkedAt == null;

        // ── 查詢字串的值不可信任，不認得的一律改回預設 ──
        private static string NormalizeTab(string? tab) => tab switch
        {
            QueueTab.All => QueueTab.All,
            QueueTab.Pending => QueueTab.Pending,
            QueueTab.Completed => QueueTab.Completed,
            QueueTab.Spam    => QueueTab.Spam,
            _                => QueueTab.Unread
        };

        private static string NormalizeSource(string? source) => source switch
        {
            QueueSource.Visit   => QueueSource.Visit,
            QueueSource.Booking => QueueSource.Booking,
            _                   => QueueSource.All
        };

        private static string NormalizeRange(string? range) => range switch
        {
            QueueRange.Today    => QueueRange.Today,
            QueueRange.Week     => QueueRange.Week,
            QueueRange.Quarter  => QueueRange.Quarter,
            QueueRange.HalfYear => QueueRange.HalfYear,
            QueueRange.All      => QueueRange.All,
            _                   => QueueRange.Default   // 不認得就回預設（一個月內）
        };

        private static string NormalizeSearchField(string? field) => field switch
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
        private static List<string> SplitKeywords(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return new List<string>();

            char[] separators = { ' ', '　', '\t' };

            return keyword.Split(separators,
                                 StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Distinct()
                          .Take(QueueSearchField.MaxKeywords)
                          .ToList();
        }

        /// <summary>
        /// 回傳 null 代表「是員工，繼續」。
        /// 約束要求 EmployeeId > 0，所以 0 也擋。
        /// ⚠️ 不用 Forbid()：Cookie 驗證的 Forbid() 會變成 302 導向登入頁，
        ///    而這幾個都是 axios 呼叫的端點，跟著導向拿回 200 + HTML，
        ///    前端會把失敗當成功。這裡固定回 403 + JSON。
        /// </summary>
        private IActionResult? RejectIfNotEmployee()
        {
            if (_currentUser.EmployeeId is null or <= 0)
                return StatusCode(403, ApiResultVM.Fail("請先以員工身分登入", "NotEmployee"));
            return null;
        }

        /// <summary>
        /// 回傳 null 代表「找得到、狀態也對，繼續」。
        /// rule 傳入上面的 CanMarkRead 或 CanHandle（把方法當參數傳進來）。
        /// </summary>
        private IActionResult? RejectIfCannot(ReviewMain? review, Func<ReviewMain, bool> rule)
        {
            if (review == null)
                return NotFound(ApiResultVM.Fail("找不到這則評論", "NotFound"));

            // 409 Conflict：請求本身沒錯，但資料的狀態已經不允許這個操作。
            // 最常見的情況是另一位員工剛處理掉這則評論。
            if (!rule(review))
                return Conflict(ApiResultVM.Fail("這則評論的狀態已經改變，請重新整理清單", "StateChanged"));

            return null;
        }

        // ════════════════════════════════════════════════════════
        //  第二區：組 ViewModel
        // ════════════════════════════════════════════════════════

        private static IQueryable<ReviewMain> ApplySource(IQueryable<ReviewMain> q, string source) => source switch
        {
            QueueSource.Visit   => q.Where(r => r.ReviewPerVisitId != null),
            QueueSource.Booking => q.Where(r => r.ReviewPerBookingId != null),
            _                   => q
        };

        /// <summary>
        /// 時間範圍篩選。
        ///
        /// byRepliedAt 決定比哪個欄位：已完成清單關心「什麼時候處理的」（RepliedAt），
        /// 其他清單關心「顧客什麼時候寫的」（CreatedAt）。
        ///
        /// todayStart 是「今天 00:00」，由呼叫端算好傳進來。
        /// 用今天 00:00 而不是此刻往前推，是因為後者會讓同一天早上跟晚上查到
        /// 不同的結果，員工會覺得清單莫名其妙在跳。
        ///
        /// 順帶一提，這個篩選也是搜尋的效能保險：先用 CreatedAt 的索引把範圍
        /// 縮小，後面的 LIKE 就只需要比對範圍內那些列，而不是整張表。
        /// </summary>
        private static IQueryable<ReviewMain> ApplyRange(
            IQueryable<ReviewMain> q, string range, bool byRepliedAt, DateTime todayStart)
        {
            int? days = QueueRange.DaysOf(range);
            if (days == null) return q;          // 全部：不篩

            DateTime from = todayStart.AddDays(-days.Value);

            return byRepliedAt
                ? q.Where(r => r.RepliedAt != null && r.RepliedAt >= from)
                : q.Where(r => r.CreatedAt >= from);
        }

        /// <summary>
        /// 關鍵字搜尋。多個關鍵字之間是「而且」，作法是一個關鍵字疊一次 Where。
        ///
        /// ⚠️ 全部都是 LINQ 查詢，EF Core 會翻成帶參數的 SQL，關鍵字是「參數值」
        ///    不是拼進字串裡的片段，所以不會有 SQL Injection。要小心的是
        ///    FromSqlRaw 那種手寫 SQL，這裡完全沒有用到。
        ///
        /// ⚠️ foreach 的迴圈變數在 C# 5 之後每次迭代都是新的變數，
        ///    所以直接在 Lambda 裡用 kw 不會發生「所有條件都變成最後一個關鍵字」
        ///    的閉包陷阱，不需要另外複製一份。
        /// </summary>
        private IQueryable<ReviewMain> ApplySearch(
            IQueryable<ReviewMain> q, string field, List<string> keywords)
        {
            if (keywords.Count == 0) return q;

            switch (field)
            {
                // ── 訂單編號 ──────────────────────────────────
                //  ReviewMain 身上沒有 OrderId，要繞兩條路才連得到訂單：
                //    預約評論 → ReviewPerBooking.OrderId
                //    現場評論 → EntryTicket.Qrtoken 對到 ReviewPerVisit.Qrtoken
                //  所以先把符合的訂單查出來，再反查兩種憑證的 id。
                case QueueSearchField.OrderNo:
                    {
                        var orderQuery = _db.Orders.AsQueryable();
                        foreach (var kw in keywords)
                            orderQuery = orderQuery.Where(o => o.OrderNo.Contains(kw));

                        var orderIds = orderQuery.Select(o => o.OrderId).ToList();

                        var bookingIds = _db.ReviewPerBookings
                                            .Where(b => orderIds.Contains(b.OrderId))
                                            .Select(b => b.ReviewPerBookingId)
                                            .ToList();

                        var tokens = _db.EntryTickets
                                        .Where(t => orderIds.Contains(t.OrderId))
                                        .Select(t => t.Qrtoken)
                                        .ToList();

                        var visitIds = _db.ReviewPerVisits
                                          .Where(v => tokens.Contains(v.Qrtoken))
                                          .Select(v => v.ReviewPerVisitId)
                                          .ToList();

                        return q.Where(r =>
                            (r.ReviewPerBookingId != null && bookingIds.Contains(r.ReviewPerBookingId.Value))
                         || (r.ReviewPerVisitId   != null && visitIds.Contains(r.ReviewPerVisitId.Value)));
                    }

                // ── 處理人員姓名 ──────────────────────────────
                //  Employees 沒有姓名欄位，姓名在 Users，所以先用姓名找出
                //  符合的員工 id，再拿去比對三個「誰處理的」欄位。
                case QueueSearchField.Employee:
                    {
                        var userQuery = _db.Users.AsQueryable();
                        foreach (var kw in keywords)
                            userQuery = userQuery.Where(u => u.Name.Contains(kw));

                        var employeeIds = (from e in _db.Employees
                                           join u in userQuery on e.UserId equals u.UserId
                                           select e.EmployeeId).ToList();

                        return q.Where(r =>
                            (r.ReadByEmployeeId       != null && employeeIds.Contains(r.ReadByEmployeeId.Value))
                         || (r.RepliedByEmployeeId    != null && employeeIds.Contains(r.RepliedByEmployeeId.Value))
                         || (r.SpamMarkedByEmployeeId != null && employeeIds.Contains(r.SpamMarkedByEmployeeId.Value)));
                    }

                // ── 評論內容或館方回覆 ────────────────────────
                default:
                    foreach (var kw in keywords)
                        q = q.Where(r => (r.ReviewContent != null && r.ReviewContent.Contains(kw))
                                      || (r.ReplyContent  != null && r.ReplyContent.Contains(kw)));
                    return q;
            }
        }

        /// <summary>
        /// 各清單的條件與排序。排序照資料庫現有的索引寫：
        ///   未讀   CreatedAt ASC               先到先處理，最舊的才不會被埋掉
        ///   待回覆 IsPinned DESC, CreatedAt ASC
        ///   已完成 RepliedAt DESC              回頭查用，最近處理的在上面
        ///   垃圾桶 SpamMarkedAt DESC           最近標記的在上面，方便複查
        /// </summary>
        private static IQueryable<ReviewMain> ApplyTab(IQueryable<ReviewMain> q, string tab) => tab switch
        {
            QueueTab.All     => q.OrderByDescending(r => r.IsPinned)
                                 .ThenBy(r => r.CreatedAt),
            QueueTab.Pending => q.Where(PendingRule)
                                 .OrderByDescending(r => r.IsPinned)
                                 .ThenBy(r => r.CreatedAt),
            // 已完成是「回頭查」用的清單，最近處理的放最上面
            QueueTab.Completed => q.Where(r => r.RepliedAt != null)
                                 .OrderByDescending(r => r.RepliedAt),
            QueueTab.Spam    => q.Where(SpamRule)
                                 .OrderByDescending(r => r.SpamMarkedAt),
            _                => q.Where(UnreadRule)
                                 .OrderBy(r => r.CreatedAt)
        };

        private static OverdueLevel GetOverdueLevel(ReviewMain r, DateTime now)
        {
            if (r.RepliedAt != null || r.SpamMarkedAt != null) return OverdueLevel.None;

            var age = now - r.CreatedAt;
            if (age >= TimeSpan.FromDays(ReviewPolicy.PublicBufferDays)) return OverdueLevel.Overdue;
            if (age >= TimeSpan.FromDays(ReviewPolicy.OverdueWarnDays))  return OverdueLevel.Soon;
            return OverdueLevel.None;
        }

        /// <summary>
        /// 一次查好清單要用的所有名稱。
        ///
        /// 沒有導覽屬性，所以不在 LINQ 裡一路 join，而是：
        ///   1. 先把這一頁的評論查出來
        ///   2. 收集要查的 id，每張表各查一次，放進 Dictionary
        ///   3. 組 VM 時用 id 去 Dictionary 拿
        /// 不管清單有幾筆，每張表都只查一次。
        /// </summary>
        private sealed record QueueLookups(
            Dictionary<int, ReviewPerVisit> Visits,
            Dictionary<int, string> VenueNames,
            Dictionary<int, ReviewPerBooking> Bookings,
            Dictionary<int, string> OrderNos,
            Dictionary<string, int> VisitOrderIds,
            Dictionary<int, string> UserNames,
            Dictionary<int, string> EmployeeNames);

        private QueueLookups LoadLookups(List<ReviewMain> reviews)
        {
            // ── 現場評論 → 憑證 → 場地 ──
            var visitIds = reviews.Where(r => r.ReviewPerVisitId != null)
                                  .Select(r => r.ReviewPerVisitId!.Value)
                                  .Distinct().ToList();
            var visits = _db.ReviewPerVisits
                            .Where(v => visitIds.Contains(v.ReviewPerVisitId))
                            .ToDictionary(v => v.ReviewPerVisitId);

            var venueIds = visits.Values.Select(v => v.VenueId).Distinct().ToList();
            var venueNames = _db.Venues
                                .Where(v => venueIds.Contains(v.VenueId))
                                .ToDictionary(v => v.VenueId, v => v.VenueName);

            // ── 現場評論 → 票券 → 訂單 ──
            //  分組要用 OrderId，但 ReviewPerVisit 沒有存 OrderId，
            //  只能靠 Qrtoken 去 EntryTickets 對。
            //  EntryTickets.Qrtoken 有唯一約束（UQ_Qrtoken），所以
            //  ToDictionary 不會撞到重複鍵。
            var visitTokens = visits.Values.Select(v => v.Qrtoken).Distinct().ToList();
            var visitOrderIds = _db.EntryTickets
                                   .Where(t => visitTokens.Contains(t.Qrtoken))
                                   .ToDictionary(t => t.Qrtoken, t => t.OrderId);

            // ── 預約評論 → 憑證 → 訂單 ──
            var bookingIds = reviews.Where(r => r.ReviewPerBookingId != null)
                                    .Select(r => r.ReviewPerBookingId!.Value)
                                    .Distinct().ToList();
            var bookings = _db.ReviewPerBookings
                              .Where(b => bookingIds.Contains(b.ReviewPerBookingId))
                              .ToDictionary(b => b.ReviewPerBookingId);

            // 訂單編號一次查完：預約評論的訂單 + 現場評論的訂單
            var orderIds = bookings.Values.Select(b => b.OrderId)
                                   .Concat(visitOrderIds.Values)
                                   .Distinct().ToList();
            var orderNos = _db.Orders
                              .Where(o => orderIds.Contains(o.OrderId))
                              .ToDictionary(o => o.OrderId, o => o.OrderNo);

            // ── 實名評論的會員姓名 ──
            var userIds = reviews.Where(r => !r.IsAnonymous && r.UserId != null)
                                 .Select(r => r.UserId!.Value)
                                 .Distinct().ToList();
            var userNames = _db.Users
                               .Where(u => userIds.Contains(u.UserId))
                               .ToDictionary(u => u.UserId, u => u.Name);

            // ── 員工姓名：Employees 本身沒有姓名，要再接 Users ──
            //    Employees.EmployeeId 與 Employees.UserId 都是 int NOT NULL，
            //    Users.UserId 也是 int，所以 join 接得起來。
            var employeeIds = reviews.SelectMany(r => new[] { r.ReadByEmployeeId, r.RepliedByEmployeeId, r.SpamMarkedByEmployeeId })
                                     .Where(id => id != null)
                                     .Select(id => id!.Value)
                                     .Distinct().ToList();
            var employeeNames = (from e in _db.Employees
                                 join u in _db.Users on e.UserId equals u.UserId
                                 where employeeIds.Contains(e.EmployeeId)
                                 select new { e.EmployeeId, u.Name })
                                .ToDictionary(x => x.EmployeeId, x => x.Name);

            return new QueueLookups(visits, venueNames, bookings, orderNos,
                                    visitOrderIds, userNames, employeeNames);
        }

        private static ReviewQueueItemVM BuildItem(ReviewMain r, QueueLookups lk, DateTime now)
        {
            // 用 id 去字典拿資料。「is int vid」的意思是：有值的話取出來叫 vid。
            ReviewPerVisit? visit = r.ReviewPerVisitId is int vid
                                    ? lk.Visits.GetValueOrDefault(vid) : null;
            ReviewPerBooking? booking = r.ReviewPerBookingId is int bid
                                        ? lk.Bookings.GetValueOrDefault(bid) : null;

            // 兩種評論都要追到訂單：預約評論直接有，現場評論靠票券轉一手。
            // 追不到就是 null，分組時會單獨列出來。
            int? orderId = null;
            if (booking != null)
            {
                orderId = booking.OrderId;
            }
            else if (visit != null && lk.VisitOrderIds.TryGetValue(visit.Qrtoken, out int visitOrderId))
            {
                orderId = visitOrderId;
            }

            string displayName = r.IsAnonymous
                ? (r.AnonymousNickname ?? "匿名使用者")
                : (r.UserId is int uid ? lk.UserNames.GetValueOrDefault(uid) : null) ?? "已停用的帳號";

            return new ReviewQueueItemVM
            {
                ReviewId      = r.ReviewId,
                StarRating    = r.StarRating,
                CreatedAt     = r.CreatedAt,
                DisplayName   = displayName,
                IsAnonymous   = r.IsAnonymous,
                IsPublic      = r.IsPublic,
                Content       = r.ReviewContent,
                MentionsVenue = r.MentionsVenue,
                MentionsStaff = r.MentionsStaff,

                IsBookingReview = r.ReviewPerBookingId != null,
                VenueName       = visit != null ? lk.VenueNames.GetValueOrDefault(visit.VenueId) : null,
                RentStartTime   = visit?.RentStartTime,
                OrderId         = orderId,
                OrderNo         = orderId is int oid ? lk.OrderNos.GetValueOrDefault(oid) : null,

                ReadAt             = r.ReadAt,
                ReadByEmployeeName = r.ReadByEmployeeId is int rid
                                     ? lk.EmployeeNames.GetValueOrDefault(rid) : null,
                IsPinned           = r.IsPinned,

                                RepliedAt             = r.RepliedAt,
                ReplyContent          = r.ReplyContent,
                RepliedByEmployeeName = r.RepliedByEmployeeId is int pid
                                        ? lk.EmployeeNames.GetValueOrDefault(pid) : null,
                ReplyViewedAt         = r.ReplyViewedAt,
                ReplySatisfaction     = r.ReplySatisfaction,

                SpamMarkedAt             = r.SpamMarkedAt,
                SpamReasonText           = r.SpamMarkedAt != null ? ReviewPolicy.SpamReasonText(r.SpamReason) : null,
                SpamMarkedByEmployeeName = r.SpamMarkedByEmployeeId is int sid
                                           ? lk.EmployeeNames.GetValueOrDefault(sid) : null,

                OverdueLevel = GetOverdueLevel(r, now)
            };
        }

        /// <summary>
        /// 把清單依訂單包成一組一組。在記憶體做，不在資料庫做——
        /// 因為組的排序鍵是「組內排最上面那一則的時間」，要先排完組內才知道。
        /// 清單已經有分頁之前的筆數上限概念（目前沒有分頁），所以這裡的成本可以接受。
        ///
        /// 排序規則：
        ///   1. 組內有任一則被置頂 → 整組浮到最上面
        ///      （否則置頂的那則會被埋進它所屬的組裡，置頂就失去意義了）
        ///   2. 追不到訂單的排最後
        ///   3. 其餘用組內排最上面那一則的時間，方向跟該清單原本的排序一致
        /// </summary>
        private static List<ReviewQueueGroupVM> BuildGroups(List<ReviewQueueItemVM> items, string tab)
        {
            bool byReplied = tab == QueueTab.Completed;

            var groups = items
                // 追不到訂單的各自成一組：用「負的 ReviewId」當臨時分組鍵，
                // OrderId 一定是正的，所以不會撞在一起。
                .GroupBy(i => i.OrderId ?? -i.ReviewId)
                .Select(g => new ReviewQueueGroupVM
                {
                    OrderId = g.Key > 0 ? g.Key : null,
                    OrderNo = g.Select(i => i.OrderNo).FirstOrDefault(n => n != null),
                    Items   = byReplied
                              ? g.OrderByDescending(i => i.RepliedAt).ToList()
                              : g.OrderBy(i => i.CreatedAt).ToList()
                })
                .ToList();

            return byReplied
                ? groups.OrderByDescending(g => g.HasPinned)
                        .ThenBy(g => g.OrderId == null)
                        .ThenByDescending(g => g.Items[0].RepliedAt)
                        .ToList()
                : groups.OrderByDescending(g => g.HasPinned)
                        .ThenBy(g => g.OrderId == null)
                        .ThenBy(g => g.Items[0].CreatedAt)
                        .ToList();
        }

        private ReviewQueueVM BuildQueueVm(
            string? tab, string? source, string? range,
            string? field, string? keyword, bool grouped)
        {
            string t = NormalizeTab(tab);
            string s = NormalizeSource(source);
            string rg = NormalizeRange(range);
            string f = NormalizeSearchField(field);
            var keywords = SplitKeywords(keyword);

            // 今天 00:00。整個請求共用同一個基準，清單和三個數字才會一致。
            DateTime todayStart = DateTime.Today;
            DateTime now = DateTime.Now;

            var bySource = ApplySource(_db.ReviewMains, s);

            // 分頁上的數字：套用來源、時間、搜尋，但不套用 tab
            //（因為它們本來就是「各個 tab 各有幾則」）。
            // 數字一律以 CreatedAt 算時間範圍，因為未讀／待回覆／垃圾都還沒有 RepliedAt。
            var countBase = ApplySearch(
                                ApplyRange(bySource, rg, byRepliedAt: false, todayStart),
                                f, keywords);

            // 清單：已完成用 RepliedAt 算時間範圍，其餘用 CreatedAt。
            var listBase = ApplySearch(
                               ApplyRange(bySource, rg, byRepliedAt: t == QueueTab.Completed, todayStart),
                               f, keywords);

            // ⚠️ 期中沒有分頁。資料量變大之後要補 Skip / Take。
            var reviews = ApplyTab(listBase, t).ToList();
            var lookups = LoadLookups(reviews);

            var items = reviews.Select(r => BuildItem(r, lookups, now)).ToList();

            return new ReviewQueueVM
            {
                Tab         = t,
                Source      = s,
                Range       = rg,
                SearchField = f,
                Keyword     = keyword ?? "",
                Keywords    = keywords,
                Grouped     = grouped,

                Items  = items,
                Groups = grouped ? BuildGroups(items, t) : new List<ReviewQueueGroupVM>(),

                UnreadCount  = countBase.Count(UnreadRule),
                PendingCount = countBase.Count(PendingRule),
                SpamCount    = countBase.Count(SpamRule),

                // 每次載入順序不同，員工比較不會永遠點第一個
                CannedReplies = ReviewPolicy.CannedReplies
                                            .OrderBy(_ => Random.Shared.Next())
                                            .ToList()
            };
        }

        // ════════════════════════════════════════════════════════
        //  第三區：寫入
        //  只改實體的值，SaveChanges 由 Action 呼叫。
        // ════════════════════════════════════════════════════════

        private static void ApplyRead(ReviewMain r, int employeeId)
        {
            r.ReadAt = DateTime.Now;
            r.ReadByEmployeeId = employeeId;
        }

        private static void ApplyReply(ReviewMain r, string content, int employeeId)
        {
            r.ReplyContent = content;
            r.RepliedAt = DateTime.Now;
            r.RepliedByEmployeeId = employeeId;
            r.IsPinned = false;     // 回覆後就離開待回覆清單，置頂沒有意義了
        }

        private static void ApplySpam(ReviewMain r, byte reason, int employeeId)
        {
            r.SpamMarkedAt = DateTime.Now;
            r.SpamMarkedByEmployeeId = employeeId;
            r.SpamReason = reason;
            r.IsPublic = false;     // 規則：垃圾強制不公開
            r.IsPinned = false;
        }

        // ════════════════════════════════════════════════════════
        //  第四區：Action
        // ════════════════════════════════════════════════════════

        /// <summary>整頁。第一次進來、按 F5、從書籤打開都走這裡。</summary>
        [HttpGet]
        public IActionResult Index(string? tab, string? source, string? range,
                                   string? field, string? keyword, bool grouped = false)
        {
            return View(BuildQueueVm(tab, source, range, field, keyword, grouped));
        }

        /// <summary>只有清單那一塊。切換篩選、搜尋、處理完一則之後，由 axios 呼叫。</summary>
        [HttpGet]
        public IActionResult QueueList(string? tab, string? source, string? range,
                                       string? field, string? keyword, bool grouped = false)
        {
            return PartialView("_QueueList", BuildQueueVm(tab, source, range, field, keyword, grouped));
        }

        /// <summary>
        /// 展開時記錄閱覽。
        /// 已經有人讀過不算錯誤：兩位員工同時展開同一則時，
        /// 第二個人不該看到錯誤訊息。Data 回傳「這次是不是新讀的」，
        /// 前端用它決定要不要調整分頁上的數字。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult MarkRead(int id)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            if (review != null && review.ReadAt != null)
                return Ok(ApiResult<bool>.Ok(false));

            var reject = RejectIfCannot(review, CanMarkRead);
            if (reject != null) return reject;

            ApplyRead(review!, _currentUser.EmployeeId!.Value);
            _db.SaveChanges();
            return Ok(ApiResult<bool>.Ok(true));
        }

        /// <summary>
        /// 設定置頂。前端送「要變成什麼」，不是「反過來」，
        /// 理由同顧客端的公開開關（實作建議書 11.20）。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult TogglePin(int id, bool isPinned)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            review!.IsPinned = isPinned;
            _db.SaveChanges();
            return Ok(ApiResultVM.Ok());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reply(int id, string? content)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            // 前端的 required、maxlength 擋不住直接送請求的人，這裡是最後一道
            // 全部是空白也不行：CHK_ReviewMain_Content_NotBlank 要求 ReplyContent 長度 > 0
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(ApiResultVM.Fail("請輸入回覆內容", "EmptyContent"));

            content = content.Trim();
            if (content.Length > ReviewPolicy.ReplyMaxLength)
                return BadRequest(ApiResultVM.Fail($"回覆不可超過 {ReviewPolicy.ReplyMaxLength} 字", "TooLong"));

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplyReply(review!, content, _currentUser.EmployeeId!.Value);
            _db.SaveChanges();
            return Ok(ApiResultVM.Ok("回覆已送出"));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult MarkSpam(int id, byte? reason)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            if (reason == null || reason >= ReviewPolicy.SpamReasons.Length)
                return BadRequest(ApiResultVM.Fail("請選擇標記理由", "InvalidReason"));

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplySpam(review!, reason.Value, _currentUser.EmployeeId!.Value);
            _db.SaveChanges();
            return Ok(ApiResultVM.Ok("已標記為垃圾"));
        }

    }
}
