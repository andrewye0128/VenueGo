using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.ReviewTickets;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// <see cref="IReviewEligibilityService"/> 的實作。
    /// 規則照搬原本 CReviewApiController 的 ResolveVisitTicketAsync／ResolveBookingTicketAsync，
    /// 只改了現場評論的部分（10/9）：用憑證 Id 取代 QRToken，並且限定會員本人。
    /// </summary>
    public sealed class ReviewEligibilityService(
        dbVenueContext db,
        ITimeService timeService,
        IBookingReviewTicketFactory bookingTicketFactory,
        IReviewTicketBackfill ticketBackfill) : IReviewEligibilityService
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;
        private readonly IBookingReviewTicketFactory _bookingTicketFactory = bookingTicketFactory;
        private readonly IReviewTicketBackfill _ticketBackfill = ticketBackfill;

        /// <inheritdoc/>
        public async Task<ReviewEligibility<ReviewPerVisit>> CheckVisitAsync(int reviewPerVisitId, int? userId)
        {
            if (reviewPerVisitId <= 0 || userId is not int uid)
                return ReviewEligibility<ReviewPerVisit>.NotFound;

            // 「是本人的」直接寫進查詢條件：不是本人就跟查不到一樣
            // 10/12：建立時間晚於網站時間的憑證（時光機撥回來之後）也當成查不到
            var ticket = await _db.ReviewPerVisits
                                  .Where(ReviewTimeline.VisitTicketIssuedBy(_timeService.Now))
                                  .FirstOrDefaultAsync(v => v.ReviewPerVisitId == reviewPerVisitId && v.UserId == uid);
            if (ticket == null)
                return ReviewEligibility<ReviewPerVisit>.NotFound;

            // 現場憑證不做「查不到就補建」：憑證是報到時依票券建立的，沒有憑證 Id 也就無從補建起。
            // 漏建的補償放在兩個地方：離場時（ReviewTicketFactory.RecordVisitEndTimeAsync）和「我的評論」清單（ListPendingAsync，10/11）。
            var existing = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            return Decide(ticket, existing, ticket.ExpiredAt);
        }

        /// <inheritdoc/>
        public async Task<ReviewEligibility<ReviewPerBooking>> CheckBookingAsync(int orderId, int? userId)
        {
            if (orderId <= 0 || userId is not int uid)
                return ReviewEligibility<ReviewPerBooking>.NotFound;

            // 10/12：建立時間晚於網站時間的憑證當成查不到。這種憑證存在時，下面的補建會被工廠的防重複擋下來，結果一樣是查無
            var ticket = await _db.ReviewPerBookings
                                  .Where(ReviewTimeline.BookingTicketIssuedBy(_timeService.Now))
                                  .FirstOrDefaultAsync(b => b.OrderId == orderId);
            if (ticket == null)
            {
                // 補償前先確認訂單是本人的，不讓人用別人的訂單 Id 觸發補建
                bool isOwner = await _db.Orders.AnyAsync(o => o.OrderId == orderId && o.UserId == uid);
                if (!isOwner)
                    return ReviewEligibility<ReviewPerBooking>.NotFound;

                // 有已付款紀錄就當場補建憑證；還沒付款或補建失敗都當成查無
                if (!await _bookingTicketFactory.CreateReviewPerBookingAsync(orderId))
                    return ReviewEligibility<ReviewPerBooking>.NotFound;

                ticket = await _db.ReviewPerBookings.FirstOrDefaultAsync(b => b.OrderId == orderId);
                if (ticket == null)
                    return ReviewEligibility<ReviewPerBooking>.NotFound;
            }

            if (ticket.UserId != uid)
                return ReviewEligibility<ReviewPerBooking>.NotFound;

            var existing = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewPerBookingId == ticket.ReviewPerBookingId);
            return Decide(ticket, existing, ticket.ExpiredAt);
        }

        /// <inheritdoc/>
        public async Task<PendingReviewTickets> ListPendingAsync(int userId)
        {
            // 10/11 方案 A：先替本人補建漏掉的憑證，下面的查詢才看得到。
            // 規則（含「期限從報到／付款時間起算、已過期不建」）都在工廠裡，這裡不重寫。
            // 代價是這支 GET 可能寫入資料；預約評論撰寫頁的「查不到就補建」本來就是這樣，不是新的做法。
            await _ticketBackfill.BackfillVisitTicketsAsync(userId);
            await _ticketBackfill.BackfillBookingTicketsAsync(userId);

            DateTime now = _timeService.Now;

            // 「還沒過期」跟 Decide 的規則一致：now >= ExpiredAt 算過期，所以還能評的是 ExpiredAt > now。
            // 「還沒評過」：ReviewMain 裡沒有指到這張憑證的評論。
            // 10/12：建立時間晚於網站時間的憑證不列出來（ReviewTimeline）
            var visits = await _db.ReviewPerVisits
                .Where(ReviewTimeline.VisitTicketIssuedBy(now))
                .Where(v => v.UserId == userId
                            && v.ExpiredAt > now
                            && !_db.ReviewMains.Any(r => r.ReviewPerVisitId == v.ReviewPerVisitId))
                .OrderBy(v => v.ExpiredAt)
                .ToListAsync();

            var bookings = await _db.ReviewPerBookings
                .Where(ReviewTimeline.BookingTicketIssuedBy(now))
                .Where(b => b.UserId == userId
                            && b.ExpiredAt > now
                            && !_db.ReviewMains.Any(r => r.ReviewPerBookingId == b.ReviewPerBookingId))
                .OrderBy(b => b.ExpiredAt)
                .ToListAsync();

            return new PendingReviewTickets(visits, bookings);
        }

        /// <summary>憑證存在而且是本人的之後：評論在未來 → 已評過 → 已過期 → 可以評，依序判斷。</summary>
        private ReviewEligibility<TTicket> Decide<TTicket>(TTicket ticket, ReviewMain? existing, DateTime expiredAt)
            where TTicket : class
        {
            // 10/12：評論的時間在未來（時光機撥回來之後）→ 當成查不到。
            //   不能回「可以評」：資料庫已經有一則了，再寫一則會撞唯一約束。
            //   也不能回「已評過」：前台會轉去單筆頁，單筆頁又查不到這則（被 ReviewTimeline 過濾掉）。
            if (existing != null && !ReviewTimeline.HasHappened(existing, _timeService.Now))
                return ReviewEligibility<TTicket>.NotFound;

            if (existing != null)
                return new(ReviewEligibilityState.AlreadyReviewed, ticket, existing);

            if (_timeService.Now >= expiredAt)
                return new(ReviewEligibilityState.Expired, ticket, null);

            return new(ReviewEligibilityState.Ok, ticket, null);
        }
    }
}
