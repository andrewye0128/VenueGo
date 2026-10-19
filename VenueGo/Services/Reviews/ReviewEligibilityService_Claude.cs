using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// <see cref="IReviewEligibilityService"/> 的實作。
    /// <para>
    /// 10/15 Reviews 重構：整支重寫。以前查「憑證存不存在」，現在每次從票券、入場紀錄、訂單、付款算出
    /// <see cref="ReviewTarget"/>。規則沿用原本憑證工廠的 1～5（規則 1 的「領票人 ReceivedUserId」改成「持票人 UserId」，
    /// 因為 ReceivedUserId 之後會棄用，持票人已經包含領票和還沒分票的情況）。
    /// </para>
    /// </summary>
    public sealed class ReviewEligibilityService(dbVenueContext db, ITimeService timeService) : IReviewEligibilityService
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;

        /// <inheritdoc/>
        public async Task<ReviewEligibility> CheckVisitAsync(int orderId, int? userId)
        {
            if (orderId <= 0 || userId is not int uid)
                return ReviewEligibility.NotFound;

            var existing = await FindExistingAsync(uid, orderId, ReviewKindCode.Visit);
            if (existing != null)
                return DecideExisting(existing);

            var targets = await FindVisitTargetsAsync(uid, orderId, onlyRecent: false);
            return DecideTarget(targets.FirstOrDefault());
        }

        /// <inheritdoc/>
        public async Task<ReviewEligibility> CheckBookingAsync(int orderId, int? userId)
        {
            if (orderId <= 0 || userId is not int uid)
                return ReviewEligibility.NotFound;

            var existing = await FindExistingAsync(uid, orderId, ReviewKindCode.Booking);
            if (existing != null)
                return DecideExisting(existing);

            var targets = await FindBookingTargetsAsync(uid, orderId, onlyRecent: false);
            return DecideTarget(targets.FirstOrDefault());
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewTarget>> ListPendingAsync(int userId)
        {
            DateTime now = _timeService.Now;

            var visits = await FindVisitTargetsAsync(userId, orderId: null, onlyRecent: true);
            var bookings = await FindBookingTargetsAsync(userId, orderId: null, onlyRecent: true);

            // 已經寫過的（不管時間是不是在未來）都不列：資料庫已經有一則了，再寫一則會撞唯一約束
            var reviewed = (await _db.Reviews
                                     .Where(r => r.UserId == userId)
                                     .Select(r => new { r.OrderId, r.ReviewKind })
                                     .ToListAsync())
                           .Select(r => (r.OrderId, (ReviewKindCode)r.ReviewKind))
                           .ToHashSet();

            return visits.Concat(bookings)
                         .Where(t => t.ExpiredAt > now && !reviewed.Contains((t.OrderId, t.Kind)))
                         .OrderBy(t => t.ExpiredAt)
                         .ToList();
        }

        // ════════════════════════════════════════════════════════
        //  找出評論對象
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 現場評論的對象：這位會員持有的票裡，在預約時段內有有效入場紀錄的。每筆訂單只回一個（最早入場的那張票）。
        /// </summary>
        /// <param name="userId">持票人（目前登入的會員）</param>
        /// <param name="orderId">只看這一筆訂單；null 代表全部</param>
        /// <param name="onlyRecent">只看最近 14 天內可能還沒過期的（待評論清單用，先在資料庫篩掉大部分）</param>
        private async Task<List<ReviewTarget>> FindVisitTargetsAsync(int userId, int? orderId, bool onlyRecent)
        {
            DateTime now = _timeService.Now;

            // 1. 這位會員持有的票，連同預約的日期與時段。
            //    已經被別則評論用掉的票不算（UQ_Reviews_TicketId）：例如分票之後別人用這張票評過了。
            var tickets = from t in _db.EntryTickets
                          join o in _db.Orders on t.OrderId equals o.OrderId
                          join r in _db.Reservations on o.ReservationId equals r.ReservationId
                          where t.UserId == userId
                                && !_db.Reviews.Any(rv => rv.TicketId == t.TicketId)
                          select new { t.TicketId, t.OrderId, r.BookingDate, r.StartTime, r.EndTime };

            if (orderId is int oid)
                tickets = tickets.Where(t => t.OrderId == oid);

            if (onlyRecent)
            {
                // 入場一定在預約當天，期限是入場＋14 天，所以 14 天以前（多留 1 天緩衝）的預約一定已經過期
                DateOnly earliest = DateOnly.FromDateTime(now.AddDays(-ReviewPolicy.TicketValidDays - 1));
                tickets = tickets.Where(t => t.BookingDate >= earliest);
            }

            var ticketRows = await tickets.ToListAsync();
            if (ticketRows.Count == 0) return [];

            // 2. 這些票「已經發生」的有效入場紀錄（時間不晚於網站時間）
            var ticketIds = ticketRows.Select(t => t.TicketId).ToList();
            var checkIns = await _db.CheckInLogs
                                    .Where(c => ticketIds.Contains(c.TicketId)
                                                && c.Action == (byte)CheckInAction.CheckIn
                                                && c.IsValid
                                                && c.ActionTime <= now)
                                    .Select(c => new { c.TicketId, c.ActionTime })
                                    .ToListAsync();
            var checkInsByTicket = checkIns.ToLookup(c => c.TicketId, c => c.ActionTime);

            // 3. 每張票取「預約時段內」最早的入場；每筆訂單取最早入場的那張票。
            //    時段的判斷在記憶體做：DateOnly＋TimeOnly 合成時間點，在 SQL 裡寫很繞，而且這裡的筆數很少。
            return ticketRows
                .Select(t =>
                {
                    DateTime rentStart = t.BookingDate.ToDateTime(t.StartTime);
                    DateTime rentEnd = t.BookingDate.ToDateTime(t.EndTime);
                    DateTime? firstIn = checkInsByTicket[t.TicketId]
                        .Where(at => at >= rentStart && at < rentEnd)
                        .Select(at => (DateTime?)at)
                        .Min();
                    return new { t.TicketId, t.OrderId, FirstIn = firstIn };
                })
                .Where(x => x.FirstIn != null)
                .GroupBy(x => x.OrderId)
                .Select(g => g.OrderBy(x => x.FirstIn).ThenBy(x => x.TicketId).First())
                .Select(x => new ReviewTarget(
                    ReviewKindCode.Visit, x.OrderId, userId, x.TicketId,
                    x.FirstIn!.Value, x.FirstIn.Value.AddDays(ReviewPolicy.TicketValidDays)))
                .ToList();
        }

        /// <summary>預約評論的對象：這位會員訂的、有已付款紀錄（不晚於網站時間）的訂單。期限從最早一筆付款起算。</summary>
        private async Task<List<ReviewTarget>> FindBookingTargetsAsync(int userId, int? orderId, bool onlyRecent)
        {
            DateTime now = _timeService.Now;

            var orders = _db.Orders.Where(o => o.UserId == userId);
            if (orderId is int oid)
                orders = orders.Where(o => o.OrderId == oid);

            var rows = await (from o in orders
                              let firstPaid = _db.Payments
                                                 .Where(p => p.OrderId == o.OrderId && p.PaidAt != null && p.PaidAt <= now)
                                                 .Min(p => p.PaidAt)
                              where firstPaid != null
                              select new { o.OrderId, FirstPaid = firstPaid!.Value })
                             .ToListAsync();

            if (onlyRecent)
            {
                DateTime earliest = now.AddDays(-ReviewPolicy.TicketValidDays);
                rows = rows.Where(r => r.FirstPaid > earliest).ToList();
            }

            return rows.Select(r => new ReviewTarget(
                           ReviewKindCode.Booking, r.OrderId, userId, null,
                           r.FirstPaid, r.FirstPaid.AddDays(ReviewPolicy.TicketValidDays)))
                       .ToList();
        }

        // ════════════════════════════════════════════════════════
        //  判定
        // ════════════════════════════════════════════════════════

        private Task<Review?> FindExistingAsync(int userId, int orderId, ReviewKindCode kind)
            => _db.Reviews.FirstOrDefaultAsync(r => r.UserId == userId
                                                    && r.OrderId == orderId
                                                    && r.ReviewKind == (byte)kind);

        /// <summary>
        /// 已經有一則評論：時間在未來（時光機撥回來之後）→ 當成查不到；否則 → 已評過。
        /// 不能回「可以評」：資料庫已經有一則了，再寫一則會撞唯一約束。
        /// 也不能回「已評過」：前台會轉去單筆頁，單筆頁又查不到這則（被 ReviewTimeline 過濾掉）。
        /// </summary>
        private ReviewEligibility DecideExisting(Review existing)
            => ReviewTimeline.HasHappened(existing, _timeService.Now)
                ? new(ReviewEligibilityState.AlreadyReviewed, null, existing)
                : ReviewEligibility.NotFound;

        /// <summary>還沒寫過：沒有對象 → 查無；過期 → 已過期；否則 → 可以評。</summary>
        private ReviewEligibility DecideTarget(ReviewTarget? target)
        {
            if (target == null)
                return ReviewEligibility.NotFound;

            return _timeService.Now >= target.ExpiredAt
                ? new(ReviewEligibilityState.Expired, target, null)
                : new(ReviewEligibilityState.Ok, target, null);
        }
    }
}
