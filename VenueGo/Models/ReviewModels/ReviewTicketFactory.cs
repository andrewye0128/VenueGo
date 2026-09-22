using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Services;

namespace VenueGo.Models.ReviewModels
{
    // 一個實作、兩個門：報到系統看到 IVisitReviewTicketFactory，
    // 訂單／付款系統看到 IBookingReviewTicketFactory，各自只看得到自己該叫的方法。
    //
    // 文件註解寫在「介面」上，不寫在這裡：呼叫端拿到的是介面，
    // IntelliSense 顯示的也是介面上的註解。<inheritdoc/> 讓這邊直接沿用，
    // 不會出現兩份說明各說各話的情況。
    public class ReviewTicketFactory(dbVenueContext db, ITimeService timeService)
        : IVisitReviewTicketFactory, IBookingReviewTicketFactory
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;

        /// <inheritdoc/>
        public async Task<bool> CreateReviewPerVisitAsync(string? token)
        {
            if (token == null) return false;

            if (await _db.ReviewPerVisits.AnyAsync(v => v.Qrtoken == token)) return false; // 防止重複建立

            var entry = await _db.EntryTickets.FirstOrDefaultAsync(t => t.Qrtoken == token);
            if (entry == null) return false;
            if (entry.Status != (byte)EntryTicketStatus.Used) return false; // 要先把票券狀態改為 Used 再呼叫

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == entry.OrderId);
            if (order == null) return false;

            var reservation = await _db.Reservations.FirstOrDefaultAsync(s => s.ReservationId == order.ReservationId);
            if (reservation == null) return false;

            var venue = await _db.Venues.FirstOrDefaultAsync(n => n.VenueId == reservation.VenueId);
            if (venue == null) return false;

            var newVisit = new ReviewPerVisit
            {
                Qrtoken = token,
                BookingMemberId = order.UserId,
                VenueId = reservation.VenueId,
                SportTypeId = venue.SportTypeId,
                RentStartTime = reservation.BookingDate.ToDateTime(reservation.StartTime),
                RentEndTime = reservation.BookingDate.ToDateTime(reservation.EndTime),
                ActualEndTime = null,
                CreatedAt = _timeService.Now,
                ExpiredAt = _timeService.Now.AddDays(CDictionary.DAY_評論資格期限天數)
            };

            // Add 不是 I/O，不需要 async；AddAsync 只有在用特殊主鍵產生策略時才需要。
            _db.ReviewPerVisits.Add(newVisit);
            await _db.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> RecordVisitEndTimeAsync(int? ticketId)
        {
            if (ticketId == null) return false;

            var entry = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (entry == null) return false;

            var reservation = await (from s in _db.Reservations
                                     join o in _db.Orders on s.ReservationId equals o.ReservationId
                                     where o.OrderId == entry.OrderId
                                     select s).FirstOrDefaultAsync();
            if (reservation == null) return false;

            var latestLog = await _db.CheckInLogs
                                     .Where(c => c.TicketId == ticketId)
                                     .OrderByDescending(c => c.ActionTime)
                                     .FirstOrDefaultAsync();
            if (latestLog == null) return false;

            var rentStartTime = reservation.BookingDate.ToDateTime(reservation.StartTime);
            if (latestLog.ActionTime < rentStartTime) return false; // 離場時間不可能早於租借開始時間

            var visitTicket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.Qrtoken == entry.Qrtoken);
            if (visitTicket == null) return false;

            visitTicket.ActualEndTime = latestLog.ActionTime;
            await _db.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> CreateReviewPerBookingAsync(int? orderId)
        {
            if (orderId == null) return false;

            if (await _db.ReviewPerBookings.AnyAsync(b => b.OrderId == orderId)) return false; // 防止重複建立

            var book = await (from p in _db.Payments
                              join o in _db.Orders on p.OrderId equals o.OrderId
                              where o.OrderId == orderId && p.PaidAt != null // 有付款紀錄才建立
                              select new { p, o }).FirstOrDefaultAsync();
            if (book == null) return false;

            var order = book.o;
            var payment = book.p;

            var newBooking = new ReviewPerBooking
            {
                UserId = order.UserId,
                OrderId = order.OrderId,
                PaymentMethod = payment.PaymentMethod,
                CreatedAt = _timeService.Now,
                ExpiredAt = _timeService.Now.AddDays(CDictionary.DAY_評論資格期限天數)
            };

            _db.ReviewPerBookings.Add(newBooking);
            await _db.SaveChangesAsync();

            return true;
        }
    }
}
