using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.ViewModels;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Controllers
{
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
            QueueTab.Spam => QueueTab.Spam,
            _ => QueueTab.Unread
        };

        private static string NormalizeSource(string? source) => source switch
        {
            QueueSource.Visit => QueueSource.Visit,
            QueueSource.Booking => QueueSource.Booking,
            _ => QueueSource.All
        };

        /// <summary>
        /// 回傳 null 代表「是員工，繼續」。
        /// 約束要求 EmployeeId > 0，所以 0 也擋。
        /// ⚠️ 不用 Forbid()：專案還沒有設定驗證機制，Forbid() 會直接丟例外。
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
        private IActionResult? RejectIfCannot(ReviewMain? review, Func<ReviewMain, bool> rule)
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

        private static IQueryable<ReviewMain> ApplySource(IQueryable<ReviewMain> q, string source) => source switch
        {
            QueueSource.Visit => q.Where(r => r.ReviewPerVisitId != null),
            QueueSource.Booking => q.Where(r => r.ReviewPerBookingId != null),
            _ => q
        };

        /// <summary>
        /// 各清單的條件與排序。排序照資料庫現有的三個索引寫：
        ///   未讀   CreatedAt ASC               先到先處理，最舊的才不會被埋掉
        ///   待回覆 IsPinned DESC, CreatedAt ASC
        ///   垃圾桶 SpamMarkedAt DESC            最近標記的在上面，方便複查
        /// </summary>
        private static IQueryable<ReviewMain> ApplyTab(IQueryable<ReviewMain> q, string tab) => tab switch
        {
            QueueTab.All => q.OrderByDescending(r => r.IsPinned)
                                 .ThenBy(r => r.CreatedAt),
            QueueTab.Pending => q.Where(PendingRule)
                                 .OrderByDescending(r => r.IsPinned)
                                 .ThenBy(r => r.CreatedAt),
            QueueTab.Completed => q.Where(r => r.RepliedAt != null)
                                 .OrderBy(r => r.CreatedAt),
            QueueTab.Spam => q.Where(SpamRule)
                                 .OrderByDescending(r => r.SpamMarkedAt),
            _ => q.Where(UnreadRule)
                                 .OrderBy(r => r.CreatedAt)
        };

        private static OverdueLevel GetOverdueLevel(ReviewMain r, DateTime now)
        {
            if (r.RepliedAt != null || r.SpamMarkedAt != null) return OverdueLevel.None;

            var age = now - r.CreatedAt;
            if (age >= TimeSpan.FromDays(ReviewPolicy.PublicBufferDays)) return OverdueLevel.Overdue;
            if (age >= TimeSpan.FromDays(ReviewPolicy.OverdueWarnDays)) return OverdueLevel.Soon;
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

            // ── 預約評論 → 憑證 → 訂單 ──
            var bookingIds = reviews.Where(r => r.ReviewPerBookingId != null)
                                    .Select(r => r.ReviewPerBookingId!.Value)
                                    .Distinct().ToList();
            var bookings = _db.ReviewPerBookings
                              .Where(b => bookingIds.Contains(b.ReviewPerBookingId))
                              .ToDictionary(b => b.ReviewPerBookingId);

            var orderIds = bookings.Values.Select(b => b.OrderId).Distinct().ToList();
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
            var employeeIds = reviews.SelectMany(r => new[] { r.ReadByEmployeeId, r.SpamMarkedByEmployeeId })
                                     .Where(id => id != null)
                                     .Select(id => id!.Value)
                                     .Distinct().ToList();
            var employeeNames = (from e in _db.Employees
                                 join u in _db.Users on e.UserId equals u.UserId
                                 where employeeIds.Contains(e.EmployeeId)
                                 select new { e.EmployeeId, u.Name })
                                .ToDictionary(x => x.EmployeeId, x => x.Name);

            return new QueueLookups(visits, venueNames, bookings, orderNos, userNames, employeeNames);
        }

        private static ReviewQueueItemVM BuildItem(ReviewMain r, QueueLookups lk, DateTime now)
        {
            // 用 id 去字典拿資料。「is int vid」的意思是：有值的話取出來叫 vid。
            ReviewPerVisit? visit = r.ReviewPerVisitId is int vid
                                    ? lk.Visits.GetValueOrDefault(vid) : null;
            ReviewPerBooking? booking = r.ReviewPerBookingId is int bid
                                        ? lk.Bookings.GetValueOrDefault(bid) : null;

            string displayName = r.IsAnonymous
                ? (r.AnonymousNickname ?? "匿名使用者")
                : (r.UserId is int uid ? lk.UserNames.GetValueOrDefault(uid) : null) ?? "已停用的帳號";

            return new ReviewQueueItemVM
            {
                ReviewId = r.ReviewId,
                StarRating = r.StarRating,
                CreatedAt = r.CreatedAt,
                DisplayName = displayName,
                IsAnonymous = r.IsAnonymous,
                IsPublic = r.IsPublic,
                Content = r.ReviewContent,
                MentionsVenue = r.MentionsVenue,
                MentionsStaff = r.MentionsStaff,

                IsBookingReview = r.ReviewPerBookingId != null,
                VenueName = visit != null ? lk.VenueNames.GetValueOrDefault(visit.VenueId) : null,
                RentStartTime = visit?.RentStartTime,
                OrderNo = booking != null ? lk.OrderNos.GetValueOrDefault(booking.OrderId) : null,

                ReadAt = r.ReadAt,
                ReadByEmployeeName = r.ReadByEmployeeId is int rid
                                     ? lk.EmployeeNames.GetValueOrDefault(rid) : null,
                IsPinned = r.IsPinned,

                SpamMarkedAt = r.SpamMarkedAt,
                SpamReasonText = r.SpamMarkedAt != null ? ReviewPolicy.SpamReasonText(r.SpamReason) : null,
                SpamMarkedByEmployeeName = r.SpamMarkedByEmployeeId is int sid
                                           ? lk.EmployeeNames.GetValueOrDefault(sid) : null,

                OverdueLevel = GetOverdueLevel(r, now)
            };
        }

        private ReviewQueueVM BuildQueueVm(string? tab, string? source)
        {
            string t = NormalizeTab(tab);
            string s = NormalizeSource(source);

            // 同一個來源篩選，算三個數字、查一個清單
            var bySource = ApplySource(_db.ReviewMains, s);

            // ⚠️ 期中沒有分頁。資料量變大之後要補 Skip / Take。
            var reviews = ApplyTab(bySource, t).ToList();
            var lookups = LoadLookups(reviews);
            var now = DateTime.Now;

            return new ReviewQueueVM
            {
                Tab = t,
                Source = s,
                Items = reviews.Select(r => BuildItem(r, lookups, now)).ToList(),

                UnreadCount = bySource.Count(UnreadRule),
                PendingCount = bySource.Count(PendingRule),
                SpamCount = bySource.Count(SpamRule),

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
        public IActionResult Index(string? tab, string? source)
        {
            return View(BuildQueueVm(tab, source));
        }

        /// <summary>只有清單那一塊。切換篩選、處理完一則之後，由 axios 呼叫。</summary>
        [HttpGet]
        public IActionResult QueueList(string? tab, string? source)
        {
            return PartialView("_QueueList", BuildQueueVm(tab, source));
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

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplyReply(review!, content, _currentUser.EmployeeId!.Value);
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

            var review = _db.ReviewMains.FirstOrDefault(r => r.ReviewId == id);
            var reject = RejectIfCannot(review, CanHandle);
            if (reject != null) return reject;

            ApplySpam(review!, reason.Value, _currentUser.EmployeeId!.Value);
            _db.SaveChanges();
            return Ok(ApiResult.Ok("已標記為垃圾"));
        }
    }
}