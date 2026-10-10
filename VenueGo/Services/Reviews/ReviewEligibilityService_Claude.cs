using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Services.ReviewTickets;

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
        IBookingReviewTicketFactory bookingTicketFactory) : IReviewEligibilityService
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;
        private readonly IBookingReviewTicketFactory _bookingTicketFactory = bookingTicketFactory;

        /// <inheritdoc/>
        public async Task<ReviewEligibility<ReviewPerVisit>> CheckVisitAsync(int reviewPerVisitId, int? userId)
        {
            if (reviewPerVisitId <= 0 || userId is not int uid)
                return ReviewEligibility<ReviewPerVisit>.NotFound;

            // 「是本人的」直接寫進查詢條件：不是本人就跟查不到一樣
            var ticket = await _db.ReviewPerVisits
                                  .FirstOrDefaultAsync(v => v.ReviewPerVisitId == reviewPerVisitId && v.UserId == uid);
            if (ticket == null)
                return ReviewEligibility<ReviewPerVisit>.NotFound;

            // 現場憑證不做「查不到就補建」：憑證是報到時依票券建立的，沒有憑證 Id 也就無從補建起。
            // 漏建的補償放在離場時（ReviewTicketFactory.RecordVisitEndTimeAsync）。
            var existing = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            return Decide(ticket, existing, ticket.ExpiredAt);
        }

        /// <inheritdoc/>
        public async Task<ReviewEligibility<ReviewPerBooking>> CheckBookingAsync(int orderId, int? userId)
        {
            if (orderId <= 0 || userId is not int uid)
                return ReviewEligibility<ReviewPerBooking>.NotFound;

            var ticket = await _db.ReviewPerBookings.FirstOrDefaultAsync(b => b.OrderId == orderId);
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

        /// <summary>憑證存在而且是本人的之後：已評過 → 已過期 → 可以評，依序判斷。</summary>
        private ReviewEligibility<TTicket> Decide<TTicket>(TTicket ticket, ReviewMain? existing, DateTime expiredAt)
            where TTicket : class
        {
            if (existing != null)
                return new(ReviewEligibilityState.AlreadyReviewed, ticket, existing);

            if (_timeService.Now >= expiredAt)
                return new(ReviewEligibilityState.Expired, ticket, null);

            return new(ReviewEligibilityState.Ok, ticket, null);
        }
    }
}
