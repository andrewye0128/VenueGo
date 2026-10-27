using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;   // 新增：Database.SqlQuery<T> 在這個命名空間
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Enums;        // 10/25 新增：PaymentChannel（預約評論的付款方式）
using VenueGo.Extensions;   // 10/25 新增：GetDisplayName（列舉上的中文名稱）
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.Auth;
using VenueGo.Services.Time;
using VenueGo.ViewModels.ReviewViewModels;
using static VenueGo.Models.ReviewModels.ReviewQueueRules;   // 10/6 新增：規則搬出去之後，呼叫的地方不用改名字

namespace VenueGo.Controllers
{
    [Authorize(Roles = RoleNames.BackOffice)]
    public sealed class AReviewController(dbVenueContext db, ICurrentUserService currentUserService, ITimeService timeService) : Controller
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUserService _currentUser = currentUserService;
        private readonly ITimeService _timeService = timeService;

        // ════════════════════════════════════════════════════════
        //  第一區：規則判定
        //  這一區只回答「這則評論現在屬於哪個清單、能不能做某個操作」。
        // ════════════════════════════════════════════════════════

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
                return StatusCode(403, ApiResult.Fail("請先以員工身分登入", "NotEmployee"));
            return null;
        }

        /// <summary>
        /// 回傳 null 代表「找得到、狀態也對，繼續」。
        /// rule 傳入上面的 CanMarkRead 或 CanHandle（把方法當參數傳進來）。
        /// </summary>
        // 10/15 Reviews 重構，原本：private IActionResult? RejectIfCannot(ReviewMain? review, Func<ReviewMain, bool> rule)
        private IActionResult? RejectIfCannot(Review? review, Func<Review, bool> rule)
        {
            if (review == null)
                return NotFound(ApiResult.Fail("找不到這則評論", "NotFound"));

            // 409 Conflict：請求本身沒錯，但資料的狀態已經不允許這個操作。
            // 最常見的情況是另一位員工剛處理掉這則評論。
            if (!rule(review))
                return Conflict(ApiResult.Fail("這則評論的狀態已經改變，請重新整理清單", "StateChanged"));

            return null;
        }

        // ════════════════════════════════════════════════════════
        //  第二區：組 ViewModel
        // ════════════════════════════════════════════════════════

        // 10/15 Reviews 重構，原本：private static IQueryable<ReviewMain> ApplySource(IQueryable<ReviewMain> q, string source) => source switch
        private static IQueryable<Review> ApplySource(IQueryable<Review> q, string source) => source switch
        {
            // 10/15 Reviews 重構，原本：QueueSource.Visit   => q.Where(r => r.ReviewPerVisitId != null),
            QueueSource.Visit   => q.Where(r => r.ReviewKind == (byte)ReviewKindCode.Visit),
            // 10/15 Reviews 重構，原本：QueueSource.Booking => q.Where(r => r.ReviewPerBookingId != null),
            QueueSource.Booking => q.Where(r => r.ReviewKind == (byte)ReviewKindCode.Booking),
            _                   => q
        };

        /// <summary>
        /// 運動類型與場地篩選（9/29）。
        ///
        /// ⚠️ 只篩得到現場評論：場地資料在 ReviewPerVisit 上，預約評論沒有。
        ///    所以選了運動類型或場地之後，預約評論一定不會出現——畫面上有一行字說明這件事。
        ///
        /// 用 Any(...) 而不是先把 id 查出來再 Contains：
        /// EF Core 會把它翻成 SQL 的 EXISTS 子查詢，一次查完，不必先把一堆 id 拉回記憶體。
        /// </summary>
        // 10/15 Reviews 重構，原本：private IQueryable<ReviewMain> ApplyVenue(IQueryable<ReviewMain> q, int? sportTypeId, int? venueId)
        private IQueryable<Review> ApplyVenue(IQueryable<Review> q, int? sportTypeId, int? venueId)
        {
            // 10/15 Reviews 重構，原本：
            // if (venueId != null)
            //     return q.Where(r => r.ReviewPerVisitId != null
            //                      && _db.ReviewPerVisits.Any(v => v.ReviewPerVisitId == r.ReviewPerVisitId
            //                                                   && v.VenueId == venueId));
            // if (sportTypeId != null)
            //     return q.Where(r => r.ReviewPerVisitId != null
            //                      && _db.ReviewPerVisits.Any(v => v.ReviewPerVisitId == r.ReviewPerVisitId
            //                                                   && v.SportTypeId == sportTypeId));
            // return q;   // 兩個都沒選：不篩
            // 10/15：評論身上就有 OrderId，經「訂單 → 預約 → 場地」篩。
            //   仍然只篩現場評論，維持原本的行為（畫面上那行說明不用改）；預約評論其實也查得到場地，之後想一起篩，拿掉 ReviewKind 那個條件就好。
            if (venueId != null)
                return q.Where(r => r.ReviewKind == (byte)ReviewKindCode.Visit
                                 && _db.Orders.Any(o => o.OrderId == r.OrderId
                                                     && _db.Reservations.Any(rs => rs.ReservationId == o.ReservationId
                                                                                && rs.VenueId == venueId)));
            if (sportTypeId != null)
                return q.Where(r => r.ReviewKind == (byte)ReviewKindCode.Visit
                                 && _db.Orders.Any(o => o.OrderId == r.OrderId
                                                     && _db.Reservations.Any(rs => rs.ReservationId == o.ReservationId
                                                                                && _db.Venues.Any(v => v.VenueId == rs.VenueId
                                                                                                    && v.SportTypeId == sportTypeId))));
            return q;   // 兩個都沒選：不篩
        }

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
        // 10/15 Reviews 重構，原本：private static IQueryable<ReviewMain> ApplyRange(
        private static IQueryable<Review> ApplyRange(
            // 10/15 Reviews 重構，原本：IQueryable<ReviewMain> q, string range, bool byRepliedAt, DateTime todayStart)
            IQueryable<Review> q, string range, bool byRepliedAt, DateTime todayStart)
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
        // 10/15 Reviews 重構，原本：private IQueryable<ReviewMain> ApplySearch(
        private IQueryable<Review> ApplySearch(
            // 10/15 Reviews 重構，原本：IQueryable<ReviewMain> q, string field, List<string> keywords)
            IQueryable<Review> q, string field, List<string> keywords)
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

                        // 10/15 Reviews 重構，原本：
                        // var bookingIds = _db.ReviewPerBookings
                        //                     .Where(b => orderIds.Contains(b.OrderId))
                        //                     .Select(b => b.ReviewPerBookingId)
                        //                     .ToList();

                        // // 10/9：現場憑證本身就有 OrderId，不用再透過票券的 QRToken 轉一手
                        // var visitIds = _db.ReviewPerVisits
                        //                   .Where(v => orderIds.Contains(v.OrderId))
                        //                   .Select(v => v.ReviewPerVisitId)
                        //                   .ToList();
                        // return q.Where(r =>
                        //     (r.ReviewPerBookingId != null && bookingIds.Contains(r.ReviewPerBookingId.Value))
                        //  || (r.ReviewPerVisitId   != null && visitIds.Contains(r.ReviewPerVisitId.Value)));
                        // 10/15：評論身上就有 OrderId，不用再繞兩種憑證
                        return q.Where(r => orderIds.Contains(r.OrderId));
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
        // 10/15 Reviews 重構，原本：private static IQueryable<ReviewMain> ApplyTab(IQueryable<ReviewMain> q, string tab) => tab switch
        private static IQueryable<Review> ApplyTab(IQueryable<Review> q, string tab) => tab switch
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


        /*  ══════════════════════════════════════════════════════════════════
            以下整段改用檢視表 dbo.v_ReviewFullInfo，原本的版本保留在註解裡。

            原本：七次查詢（ReviewPerVisits／Venues／EntryTickets／
                  ReviewPerBookings／Orders／Users／Employees×Users）
            現在：一次查詢

            能一次做完的原因是 View 已經在資料庫那邊把七張表接好了，
            而且每個 JOIN 都接在對方的主鍵或唯一鍵上，所以一則評論保證一列。
            ⚠️ 前提是 createV_Booking_Claude.sql 已經跑過。

            ── 原本的程式碼 ──────────────────────────────────────────────

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


            ══════════════════════════════════════════════════════════════════ */

        /// <summary>
        /// 一次查好清單要用的所有名稱，回傳「ReviewId → 周邊資料」。
        ///
        /// 跨表的工作全部在檢視表 dbo.v_ReviewFullInfo 裡完成，
        /// 這裡只負責把結果撈回來。不管清單有幾筆，都只查一次。
        ///
        /// ⚠️ SELECT 的欄位必須跟 ReviewRefRow 的屬性一一對應，
        ///    少一個或多一個都是執行期才會爆，不是編譯期。
        /// </summary>
        // 10/15 Reviews 重構，原本：private Dictionary<int, ReviewRefRow> LoadRefs(List<ReviewMain> reviews)
        private Dictionary<int, ReviewRefRow> LoadRefs(List<Review> reviews)
        {
            if (reviews.Count == 0) return new Dictionary<int, ReviewRefRow>();

            var ids = reviews.Select(r => r.ReviewId).Distinct().ToList();

            // SqlQuery 回傳的是 IQueryable，可以接著用 LINQ 疊條件——
            // EF 會把這段 SQL 當成子查詢包起來，再把 Contains 翻成 IN。
            // 所以 ids 是以參數送出去的，不是字串拼接。
            // 10/15 Reviews 重構，原本：
            // return _db.Database
            //           .SqlQuery<ReviewRefRow>($@"
            //               SELECT [ReviewId], [OrderId], [OrderNo], [VenueName], [VenueId],
            // [RentStartTime], [UserName],
            //                      [ReadByEmployeeName], [RepliedByEmployeeName],
            //                      [SpamMarkedByEmployeeName]
            //               FROM   dbo.v_ReviewFullInfo")
            //           .Where(x => ids.Contains(x.ReviewId))
            //           .ToList()
            //           .ToDictionary(x => x.ReviewId);
            // 10/15：改讀檢視表 v_ReviewAdmin（Scaffold 進來的實體），欄位對不起來會在編譯期就發現，不用等到執行期。
            return _db.VReviewAdmins
                      .Where(x => ids.Contains(x.ReviewId))
                      .Select(x => new ReviewRefRow
                      {
                          ReviewId = x.ReviewId,
                          OrderId = x.OrderId,
                          OrderNo = x.OrderNo,
                          VenueName = x.VenueName,
                          VenueId = x.VenueId,
                          RentStartTime = x.RentStartTime,
                          UserName = x.MemberName,
                          ReadByEmployeeName = x.ReadByEmployeeName,
                          RepliedByEmployeeName = x.RepliedByEmployeeName,
                          SpamMarkedByEmployeeName = x.SpamMarkedByEmployeeName,
                      })
                      .ToList()
                      .ToDictionary(x => x.ReviewId);

            // ⚠️ 如果上面這種「SqlQuery 之後再疊 LINQ」的寫法在執行期出問題
            //    （EF 要把它當子查詢包起來，極少數情況會翻譯失敗），
            //    換成下面這版，用 SQL Server 內建的 STRING_SPLIT 直接篩：
            //
            //    string joined = string.Join(',', ids);
            //    return _db.Database
            //              .SqlQuery<ReviewRefRow>($@"
            //                  SELECT v.[ReviewId], v.[OrderId], v.[OrderNo], v.[VenueName],
            //                         v.[RentStartTime], v.[UserName],
            //                         v.[ReadByEmployeeName], v.[RepliedByEmployeeName],
            //                         v.[SpamMarkedByEmployeeName]
            //                  FROM   dbo.v_ReviewFullInfo v
            //                  JOIN   STRING_SPLIT({joined}, ',') s ON CAST(s.[value] AS int) = v.[ReviewId]")
            //              .ToList()
            //              .ToDictionary(x => x.ReviewId);
            //
            //    （STRING_SPLIT 需要 SQL Server 2016 以上，且資料庫相容性層級 130 以上。）
        }

        /// <summary>
        /// 10/25：每筆訂單「用什麼付的」，回傳「OrderId → 付款方式的中文名稱」。
        /// 跟顧客端撰寫頁的確認區塊同一個規則（MemberReviewService.PaymentChannelsAsync）：
        /// 取最早一筆已付款紀錄的 PaymentChannel。一筆訂單的付款紀錄很少，整批讀回來再分組。
        /// 查不到的訂單不放進結果（不要用列舉的預設值 0，那會變成「現場櫃台」，顯示錯的付款方式）。
        /// </summary>
        private Dictionary<int, string> PaymentChannelNames(IEnumerable<int> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0) return [];

            var rows = _db.Payments
                          .Where(p => ids.Contains(p.OrderId) && p.PaidAt != null)
                          .Select(p => new { p.OrderId, p.PaidAt, p.PaymentId, p.PaymentChannel })
                          .ToList();

            return rows.GroupBy(p => p.OrderId)
                       .ToDictionary(g => g.Key,
                                     g => ((PaymentChannel)g.OrderBy(p => p.PaidAt).ThenBy(p => p.PaymentId).First().PaymentChannel)
                                              .GetDisplayName());
        }

        // 10/15 Reviews 重構，原本：private static ReviewQueueItemVM BuildItem(ReviewMain r, Dictionary<int, ReviewRefRow> refs, DateTime now)
        private static ReviewQueueItemVM BuildItem(Review r, Dictionary<int, ReviewRefRow> refs, DateTime now)
        {
            // 周邊資料全在這一列裡。理論上一定找得到（它就是從 ReviewMain 長出來的），
            // 但用 GetValueOrDefault 比較安全——真的漏掉時是欄位空白，不是整頁 500。
            ReviewRefRow? x = refs.GetValueOrDefault(r.ReviewId);

            // 「追不到訂單」的判斷沒有變：View 已經幫忙把
            // 預約評論的 OrderId 和現場評論的 QRToken→票券→OrderId 合併成一欄，
            // 兩邊都追不到就是 null，分組時會單獨列出來。

            string displayName = r.IsAnonymous
                ? (r.AnonymousNickname ?? "匿名使用者")
                : x?.UserName ?? "已停用的帳號";

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

                // 10/15 Reviews 重構，原本：IsBookingReview = r.ReviewPerBookingId != null,
                IsBookingReview = r.ReviewKind == (byte)ReviewKindCode.Booking,
                VenueName       = x?.VenueName,
                VenueId         = x?.VenueId,
                RentStartTime   = x?.RentStartTime,
                OrderId         = x?.OrderId,
                OrderNo         = x?.OrderNo,

                ReadAt             = r.ReadAt,
                ReadByEmployeeName = x?.ReadByEmployeeName,
                IsPinned           = r.IsPinned,

                                RepliedAt             = r.RepliedAt,
                ReplyContent          = r.ReplyContent,
                RepliedByEmployeeName = x?.RepliedByEmployeeName,
                ReplyViewedAt         = r.ReplyViewedAt,
                ReplySatisfaction     = r.ReplySatisfaction,

                SpamMarkedAt             = r.SpamMarkedAt,
                SpamReasonText           = r.SpamMarkedAt != null ? ReviewPolicy.SpamReasonText(r.SpamReason) : null,
                SpamMarkedByEmployeeName = x?.SpamMarkedByEmployeeName,

                // 10/26 負評優先：提醒的顏色依星等不同（ReviewUrgency_Claude.cs）。原本：
                // OverdueLevel = GetOverdueLevel(r, now)
                OverdueLevel = ReviewUrgency.LevelOf(r, now),
                Stage        = ReviewUrgency.StageOf(r),
                DueAt        = ReviewUrgency.DueOf(r),
                DueText      = ReviewUrgency.DueOf(r) is DateTime due ? ReviewUrgency.DueText(due, now) : null,
                IsPastDue    = ReviewUrgency.DueOf(r) is DateTime dueAt && now >= dueAt
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
            // 10/26：排序改由 SortItems、SortGroups 依 tab 決定，不再需要。原本：bool byReplied = tab == QueueTab.Completed;

            var groups = items
                // 追不到訂單的各自成一組：用「負的 ReviewId」當臨時分組鍵，
                // OrderId 一定是正的，所以不會撞在一起。
                .GroupBy(i => i.OrderId ?? -i.ReviewId)
                .Select(g => new ReviewQueueGroupVM
                {
                    OrderId = g.Key > 0 ? g.Key : null,
                    OrderNo = g.Select(i => i.OrderNo).FirstOrDefault(n => n != null),
                    // 10/26：組內也照清單的順序（未讀、待回覆是期限）。原本：
                    // Items   = byReplied
                    //           ? g.OrderByDescending(i => i.RepliedAt).ToList()
                    //           : g.OrderBy(i => i.CreatedAt).ToList()
                    Items   = SortItems(g, tab)
                })
                .ToList();

            // 原本：
            // return byReplied
            //     ? groups.OrderByDescending(g => g.HasPinned)
            //             .ThenBy(g => g.OrderId == null)
            //             .ThenByDescending(g => g.Items[0].RepliedAt)
            //             .ToList()
            //     : groups.OrderByDescending(g => g.HasPinned)
            //             .ThenBy(g => g.OrderId == null)
            //             .ThenBy(g => g.Items[0].CreatedAt)
            //             .ToList();
            return SortGroups(groups.OrderByDescending(g => g.HasPinned)
                                    .ThenBy(g => g.OrderId == null), tab);
        }

        /// <summary>
        /// 依場地分組（9/29）。排序規則跟依訂單分組一樣：
        ///   1. 組內有任一則被置頂 → 整組浮到最上面
        ///   2. 追不到場地的排最後（資料異常，理論上不會發生：有選運動類型時只會有現場評論）
        ///   3. 其餘用組內排最上面那一則的時間，方向跟該清單原本的排序一致
        /// </summary>
        private static List<ReviewQueueGroupVM> BuildVenueGroups(List<ReviewQueueItemVM> items, string tab)
        {
            // 10/26：排序改由 SortItems、SortGroups 依 tab 決定，不再需要。原本：bool byReplied = tab == QueueTab.Completed;

            var groups = items
                // 追不到場地的各自成一組：用「負的 ReviewId」當臨時分組鍵，VenueId 一定是正的，不會撞在一起
                .GroupBy(i => i.VenueId ?? -i.ReviewId)
                .Select(g => new ReviewQueueGroupVM
                {
                    Kind      = QueueGroupKind.Venue,
                    VenueId   = g.Key > 0 ? g.Key : null,
                    VenueName = g.Select(i => i.VenueName).FirstOrDefault(n => n != null),
                    // 10/26：組內也照清單的順序（未讀、待回覆是期限）。原本：
                    // Items     = byReplied
                    //             ? g.OrderByDescending(i => i.RepliedAt).ToList()
                    //             : g.OrderBy(i => i.CreatedAt).ToList()
                    Items     = SortItems(g, tab)
                })
                .ToList();

            // 原本：
            // return byReplied
            //     ? groups.OrderByDescending(g => g.HasPinned)
            //             .ThenBy(g => g.VenueId == null)
            //             .ThenByDescending(g => g.Items[0].RepliedAt)
            //             .ToList()
            //     : groups.OrderByDescending(g => g.HasPinned)
            //             .ThenBy(g => g.VenueId == null)
            //             .ThenBy(g => g.Items[0].CreatedAt)
            //             .ToList();
            return SortGroups(groups.OrderByDescending(g => g.HasPinned)
                                    .ThenBy(g => g.VenueId == null), tab);
        }

        /// <summary>
        /// 10/27：已完成清單依顧客對館方回覆的表態篩選（ReplySatisfaction：0 不滿意／1 普通／2 滿意／null 未表態）。
        /// 「未表態」包含顧客還沒看到回覆的。
        /// </summary>
        private static IQueryable<Review> ApplySatisfaction(IQueryable<Review> q, string satisfaction) => satisfaction switch
        {
            QueueSatisfaction.All         => q,
            QueueSatisfaction.Unsatisfied => q.Where(r => r.ReplySatisfaction == 0),
            QueueSatisfaction.Neutral     => q.Where(r => r.ReplySatisfaction == 1),
            QueueSatisfaction.Satisfied   => q.Where(r => r.ReplySatisfaction == 2),
            QueueSatisfaction.None        => q.Where(r => r.ReplySatisfaction == null),
            _ => throw new ArgumentOutOfRangeException(nameof(satisfaction), satisfaction, "未定義的滿意度篩選")
        };

        /// <summary>
        /// 10/27：已完成清單裡各種表態各有幾則。條件跟已完成清單一樣（有回覆），只查一次資料庫。
        /// 跟 ApplySatisfaction 一樣，0／1／2／null 各自寫出來，不靠 else。
        /// </summary>
        private static Dictionary<string, int> CountSatisfaction(IQueryable<Review> q)
        {
            var counts = q.Where(r => r.RepliedAt != null)
                          .GroupBy(r => r.ReplySatisfaction)
                          .Select(g => new { Satisfaction = g.Key, Count = g.Count() })
                          .ToList();

            int CountOf(byte? value) => counts.Where(c => c.Satisfaction == value).Sum(c => c.Count);

            return new Dictionary<string, int>
            {
                [QueueSatisfaction.Unsatisfied] = CountOf(0),
                [QueueSatisfaction.Neutral]     = CountOf(1),
                [QueueSatisfaction.Satisfied]   = CountOf(2),
                [QueueSatisfaction.None]        = CountOf(null)
            };
        }

        /// <summary>
        /// 10/26 負評優先：清單（或一組裡面）的順序。
        ///   未讀、待回覆：置頂 → 有文字的在前、只有星等的放最後 → 期限早的在前 → 評論早的在前。
        ///     評分越低期限越短，所以負評自然排在前面；同樣星等，越早進來的越前面（期限表在 ReviewUrgency_Claude.cs）。
        ///   已完成：回覆時間新的在前（原本的順序）。
        ///   全部、垃圾桶：評論時間舊的在前（原本分組時的順序）。
        /// </summary>
        private static List<ReviewQueueItemVM> SortItems(IEnumerable<ReviewQueueItemVM> items, string tab) => tab switch
        {
            QueueTab.Unread or QueueTab.Pending => items.OrderByDescending(i => i.IsPinned)
                                                        .ThenBy(i => i.IsRatingOnly)
                                                        .ThenBy(i => i.DueAt ?? DateTime.MaxValue)
                                                        .ThenBy(i => i.CreatedAt)
                                                        .ThenBy(i => i.ReviewId)
                                                        .ToList(),
            QueueTab.Completed => items.OrderByDescending(i => i.RepliedAt).ToList(),
            QueueTab.All or QueueTab.Spam => items.OrderBy(i => i.CreatedAt).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "未定義的清單")
        };

        /// <summary>10/26：組的順序。置頂、追不到的排最後已經排好，這裡接著用「組裡排第一的那一則」排（跟 SortItems 一致）。</summary>
        private static List<ReviewQueueGroupVM> SortGroups(IOrderedEnumerable<ReviewQueueGroupVM> groups, string tab) => tab switch
        {
            QueueTab.Unread or QueueTab.Pending => groups.ThenBy(g => g.Items[0].IsRatingOnly)
                                                         .ThenBy(g => g.Items[0].DueAt ?? DateTime.MaxValue)
                                                         .ThenBy(g => g.Items[0].CreatedAt)
                                                         .ToList(),
            QueueTab.Completed => groups.ThenByDescending(g => g.Items[0].RepliedAt).ToList(),
            QueueTab.All or QueueTab.Spam => groups.ThenBy(g => g.Items[0].CreatedAt).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "未定義的清單")
        };

        /// <summary>
        /// 運動類型與場地的查詢字串一樣不可信任（9/29）：
        ///   （9/29 舊規則，v2 已停用）場地找得到 → 運動類型一律改成「這個場地的運動類型」，兩個條件才不會互相矛盾
        ///               （例如網址被改成「羽球＋網球場 C1」）
        ///   v2：場地找得到 → 運動類型「不跟著改」：原本是全部或這個場地的類型就照舊，
        ///       其他類型（只有網址被改過才會發生，例如「羽球＋網球場 C1」）改成全部，兩個條件才不會互相矛盾。
        ///       這樣「取消場地篩選」一步就回到點場地之前的樣子。
        ///   場地找不到 → 當作沒選場地
        ///   運動類型找不到 → 當作沒選
        /// </summary>
        private (int? SportTypeId, int? VenueId, string? VenueName) NormalizeVenue(int? sportTypeId, int? venueId)
        {
            if (venueId != null)
            {
                var venue = _db.Venues
                               .Where(v => v.VenueId == venueId)
                               .Select(v => new { v.VenueId, v.SportTypeId, v.VenueName })
                               .FirstOrDefault();
                // 原本（9/29）：運動類型跟著場地走
                // if (venue != null) return (venue.SportTypeId, venue.VenueId, venue.VenueName);
                if (venue != null)
                {
                    int? sport = sportTypeId == venue.SportTypeId ? sportTypeId : null;
                    return (sport, venue.VenueId, venue.VenueName);
                }
                // 找不到這個場地：往下走，只看運動類型
            }

            if (sportTypeId != null && _db.SportTypes.Any(st => st.SportTypeId == sportTypeId))
                return (sportTypeId, null, null);

            return (null, null, null);
        }

        /// <summary>評論管理每頁幾則（10/23）。要改只改這裡，畫面上的頁數、快轉資訊都跟著算。</summary>
        private const int PageSize = 10;

        /// <summary>
        /// 這一則在目前清單裡「排序依據」的時間（10/23，長按快轉時顯示）。
        /// 跟 ApplyTab 的排序一致：已完成看回覆時間、垃圾桶看標記時間，其餘看評論時間。
        /// </summary>
        private static DateTime SortTimeOf(ReviewQueueItemVM item, string tab) => tab switch
        {
            QueueTab.All       => item.CreatedAt,
            // 10/26 未讀、待回覆改成依期限排序，所以顯示期限。原本：
            // QueueTab.Unread    => item.CreatedAt,
            // QueueTab.Pending   => item.CreatedAt,
            QueueTab.Unread    => item.DueAt ?? item.CreatedAt,
            QueueTab.Pending   => item.DueAt ?? item.CreatedAt,
            QueueTab.Completed => item.RepliedAt ?? item.CreatedAt,
            QueueTab.Spam      => item.SpamMarkedAt ?? item.CreatedAt,
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "未定義的清單")
        };

        private ReviewQueueVM BuildQueueVm(
            string? tab, string? source, string? range,
            string? field, string? keyword, bool grouped,
            int? sportTypeId, int? venueId, int? page,
            string? satisfaction = null)   // 10/27 新增：已完成清單的滿意度篩選
        {
            string t = NormalizeTab(tab);
            // 10/27：只有已完成清單用得到；其他清單一律當作「全部」，才不會被當成有篩選條件（例如不會因此列出只有星等的評論）
            string sat = t == QueueTab.Completed ? NormalizeSatisfaction(satisfaction) : QueueSatisfaction.All;
            string s = NormalizeSource(source);
            string rg = NormalizeRange(range);
            string f = NormalizeSearchField(field);
            var keywords = SplitKeywords(keyword);
            var (sport, venue, venueName) = NormalizeVenue(sportTypeId, venueId);

            // 選了運動類型就改成依場地分組，依訂單分組強制關掉（開關在畫面上也會停用）
            // 原本：bool groupByVenue = sport != null;
            // v2：在「全部」底下點場地，運動類型會維持 null，所以場地也要算進來
            bool groupByVenue = sport != null || venue != null;
            bool groupByOrder = grouped && !groupByVenue;

            // 今天 00:00。整個請求共用同一個基準，清單和三個數字才會一致。
            DateTime todayStart = _timeService.Today;
            DateTime now = _timeService.Now;

            // 10/12：時間在未來的評論（時光機撥回來之後）不列出來。員工一點開就會寫入早於建立時間的已讀時間，撞 CHK_ReviewMain_HandleTime_Logic
            // 10/15 Reviews 重構，原本：var bySource = ApplyVenue(ApplySource(_db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(now)), s), sport, venue);
            var bySource = ApplyVenue(ApplySource(_db.Reviews.Where(ReviewTimeline.ReviewHappenedBy(now)), s), sport, venue);

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

            // 10/23 分頁：先把整份清單照畫面上的順序排好，再切出這一頁。
            //   不能先切再分組：同一筆訂單的兩則評論可能隔很久（例如現場評論在第 1 頁、預約評論在第 3 頁），
            //   先切的話兩則會被拆到不同頁，分組就失去「放在一起看」的意義。
            //   所以有分組時，用「分組之後」的順序切頁；同一組被切到兩頁時，組的標題會標「接上一頁／下一頁還有」。
            //   目前資料量小，整份在記憶體裡排；資料量變大之後，不分組的情況可以改成在資料庫 Skip / Take。
            // 10/27：已完成清單各種表態的數字（套用滿意度篩選之前算，數字才跟按鈕對得上）
            Dictionary<string, int> satisfactionCounts = new();
            if (t == QueueTab.Completed) satisfactionCounts = CountSatisfaction(listBase);

            // 10/27：已完成清單依滿意度篩選
            if (t == QueueTab.Completed) listBase = ApplySatisfaction(listBase, sat);

            var reviews = ApplyTab(listBase, t).ToList();

            // 10/26：全部、已完成清單在預設條件下，不列「只有星等」的評論（跟顧客端評論專區一樣）。
            //   任一個篩選條件不是預設值（來源、時間範圍、運動類型或場地、搜尋、依訂單分組）就一起列出：例如用訂單編號搜尋時，員工要找的就是那一則。
            //   10/27：依訂單分組也算（跟重置按鈕的 HasFilter 一致）
            //   一則有文字的都沒有時全部列出，不讓畫面空著（也跟顧客端一樣）。
            //   未讀、待回覆要處理，一律列出（放在最後）；垃圾桶也一律列出：只有星等的評論不該被標成垃圾，列出來錯誤才看得到。
            bool hideRatingOnly = (t == QueueTab.All || t == QueueTab.Completed)
                                  && s == QueueSource.All
                                  && rg == QueueRange.Default
                                  && keywords.Count == 0
                                  && sport == null
                                  && venue == null
                                  && !grouped
                                  && sat == QueueSatisfaction.All;   // 10/27：選了滿意度也算篩選條件
            int ratingOnlyHidden = 0;
            if (hideRatingOnly)
            {
                var withText = reviews.Where(r => !string.IsNullOrWhiteSpace(r.ReviewContent)).ToList();
                if (withText.Count > 0)
                {
                    ratingOnlyHidden = reviews.Count - withText.Count;
                    reviews = withText;
                }
            }

            var refs = LoadRefs(reviews);   // 原本：var lookups = LoadLookups(reviews);

            var items = reviews.Select(r => BuildItem(r, refs, now)).ToList();

            // 10/26 負評優先：未讀、待回覆依期限重新排（SortItems）。期限要先算好才能排，所以在記憶體排。
            //   其他清單維持 ApplyTab 在資料庫排好的順序。
            if (t == QueueTab.Unread || t == QueueTab.Pending) items = SortItems(items, t);

            // 10/25：預約評論補上付款方式（View 對預約評論不顯示場地、使用時段，改顯示這個）
            var channelNames = PaymentChannelNames(items.Where(i => i.IsBookingReview && i.OrderId != null)
                                                        .Select(i => i.OrderId!.Value));
            items = items.Select(i => i.IsBookingReview && i.OrderId is int orderId
                                      && channelNames.TryGetValue(orderId, out var channelName)
                                          ? i with { PaymentChannelName = channelName }
                                          : i)
                         .ToList();

            bool hasGroups = groupByVenue || groupByOrder;

            // 10/26：未讀、待回覆沒分組時，最後一段「只有星等」的評論前面畫分隔線，標「只有星等（N 則）」。置頂的不算
            int ratingOnlySection = 0;
            if ((t == QueueTab.Unread || t == QueueTab.Pending) && !hasGroups)
                ratingOnlySection = items.Count(i => i.IsRatingOnly && !i.IsPinned);
            List<ReviewQueueGroupVM> allGroups = [];
            if (groupByVenue) allGroups = BuildVenueGroups(items, t);
            else if (groupByOrder) allGroups = BuildGroups(items, t);
            else if (!hasGroups) allGroups = [];   // 沒分組：不需要組

            // 畫面上的順序：有分組時是分組之後的順序，沒分組時就是清單原本的順序
            List<ReviewQueueItemVM> ordered = [];
            if (hasGroups) ordered = allGroups.SelectMany(g => g.Items).ToList();
            else if (!hasGroups) ordered = items;

            int totalPages = Math.Max(1, (ordered.Count + PageSize - 1) / PageSize);
            // 超出範圍（手動改網址、處理完最後一頁的評論）調整到範圍內，不回錯誤
            int currentPage = Math.Clamp(page ?? 1, 1, totalPages);
            int skip = (currentPage - 1) * PageSize;
            var pageItems = ordered.Skip(skip).Take(PageSize).ToList();

            // 這一頁有出現的組，只留這一頁的評論；記下這組有沒有被切到上一頁或下一頁
            List<ReviewQueueGroupVM> pageGroups = [];
            if (hasGroups)
            {
                var onPage = pageItems.Select(i => i.ReviewId).ToHashSet();
                int start = 0;
                foreach (var g in allGroups)
                {
                    int end = start + g.Items.Count;   // 這組在整份清單裡的位置：[start, end)
                    var mine = g.Items.Where(i => onPage.Contains(i.ReviewId)).ToList();
                    if (mine.Count > 0)
                    {
                        pageGroups.Add(g with
                        {
                            Items = mine,
                            TotalCount = g.Items.Count,
                            ContinuedFromPrevious = start < skip,
                            ContinuesOnNext = end > skip + PageSize
                        });
                    }
                    start = end;
                }
            }

            // 每一頁第一則的排序時間（長按快轉時顯示「正在經過哪裡」）
            var pageMarks = ordered.Where((_, i) => i % PageSize == 0)
                                   // 10/26：未讀、待回覆顯示的是期限，前面加「期限 」。原本：.Select(i => new QueuePageMark(SortTimeOf(i, t), i.IsPinned))
                                   .Select(i => new QueuePageMark(SortTimeOf(i, t), i.IsPinned,
                                                                  t == QueueTab.Unread || t == QueueTab.Pending ? "期限 " : ""))
                                   .ToList();

            return new ReviewQueueVM
            {
                Tab         = t,
                Source      = s,
                Range       = rg,
                SearchField = f,
                Keyword     = keyword ?? "",
                Keywords    = keywords,
                // 原本：Grouped     = groupByOrder,
                // v2：記住「使用者的選擇」，被場地分組擋住時也不丟掉，取消篩選後才回得去。
                //     實際有沒有依訂單分組，看 ReviewQueueVM.GroupByOrder
                Grouped     = grouped,

                SportTypeId      = sport,
                VenueId          = venue,
                VenueFilterName  = venueName,
                SportTypeOptions = _db.SportTypes
                                      .OrderBy(st => st.SportTypeId)
                                      .Select(st => new SportTypeOption(
                                          st.SportTypeId,
                                          st.IsActive ? st.SportName : st.SportName + "（已停用）"))
                                      .ToList(),

                // 10/23 分頁：Items、Groups 都只有這一頁
                Items  = pageItems,
                Groups = pageGroups,
                Page       = currentPage,
                TotalPages = totalPages,
                PageMarks  = pageMarks,
                TotalItemCount = ordered.Count,
                RatingOnlyHidden       = ratingOnlyHidden,
                RatingOnlySectionCount = ratingOnlySection,
                Satisfaction           = sat,
                SatisfactionCounts     = satisfactionCounts,

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


        // ════════════════════════════════════════════════════════
        //  第四區：Action
        // ════════════════════════════════════════════════════════

        /// <summary>整頁。第一次進來、按 F5、從書籤打開都走這裡。</summary>
        [HttpGet]
        // 10/27：多了 satisfaction（已完成清單的滿意度篩選）
        public IActionResult Index(string? tab, string? source, string? range,
                                   string? field, string? keyword, bool grouped = false,
                                   int? sportTypeId = null, int? venueId = null, int? page = null,
                                   string? satisfaction = null)
        {
            return View(BuildQueueVm(tab, source, range, field, keyword, grouped, sportTypeId, venueId, page, satisfaction));
        }

        /// <summary>只有清單那一塊。切換篩選、搜尋、處理完一則之後，由 axios 呼叫。</summary>
        [HttpGet]
        // 10/27：多了 satisfaction（已完成清單的滿意度篩選）
        public IActionResult QueueList(string? tab, string? source, string? range,
                                       string? field, string? keyword, bool grouped = false,
                                       int? sportTypeId = null, int? venueId = null, int? page = null,
                                       string? satisfaction = null)
        {
            return PartialView("_QueueList", BuildQueueVm(tab, source, range, field, keyword, grouped, sportTypeId, venueId, page, satisfaction));
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

            // 10/12：時間在未來的評論當成找不到（回 404），不讓它寫入時間錯亂的資料
            // 10/15 Reviews 重構，原本：var review = _db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var review = _db.Reviews.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            if (review != null && review.ReadAt != null)
                return Ok(ApiResult<bool>.Ok(false));

            var reject = RejectIfCannot(review, CanMarkRead);
            if (reject != null) return reject;

            ApplyRead(review!, _currentUser.EmployeeId!.Value, _timeService.Now);
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

            // 10/12：時間在未來的評論當成找不到（回 404），不讓它寫入時間錯亂的資料
            // 10/15 Reviews 重構，原本：var review = _db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var review = _db.Reviews.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            review!.IsPinned = isPinned;
            _db.SaveChanges();
            return Ok(ApiResult.Ok());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reply(int id, string? content)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            // 前端的 required、maxlength 擋不住直接送請求的人，這裡是最後一道
            // 全部是空白也不行：CHK_ReviewMain_Content_NotBlank 要求 ReplyContent 長度 > 0
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(ApiResult.Fail("請輸入回覆內容", "EmptyContent"));

            content = content.Trim();
            if (content.Length > ReviewPolicy.ReplyMaxLength)
                return BadRequest(ApiResult.Fail($"回覆不可超過 {ReviewPolicy.ReplyMaxLength} 字", "TooLong"));

            // 10/12：時間在未來的評論當成找不到（回 404），不讓它寫入時間錯亂的資料
            // 10/15 Reviews 重構，原本：var review = _db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var review = _db.Reviews.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplyReply(review!, content, _currentUser.EmployeeId!.Value, _timeService.Now);
            _db.SaveChanges();
            return Ok(ApiResult.Ok("回覆已送出"));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult MarkSpam(int id, byte? reason)
        {
            var deny = RejectIfNotEmployee();
            if (deny != null) return deny;

            if (reason == null || reason >= ReviewPolicy.SpamReasons.Length)
                return BadRequest(ApiResult.Fail("請選擇標記理由", "InvalidReason"));

            // 10/12：時間在未來的評論當成找不到（回 404），不讓它寫入時間錯亂的資料
            // 10/15 Reviews 重構，原本：var review = _db.ReviewMains.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var review = _db.Reviews.Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now)).FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplySpam(review!, reason.Value, _currentUser.EmployeeId!.Value, _timeService.Now);
            _db.SaveChanges();
            return Ok(ApiResult.Ok("已標記為垃圾"));
        }

#if DEBUG
        [HttpGet]
        public IActionResult CheckMyClaims()
        {
            // 檢查有沒有任何一筆 Claim 的型別是「角色」
            var roles = User.Claims
                            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToList();

            // 可以在這裡打斷點（Breakpoint），看 roles 陣列裡面有沒有字串（例如 "Member", "Admin"）
            return Json(roles);
        }
#endif
    }
}
