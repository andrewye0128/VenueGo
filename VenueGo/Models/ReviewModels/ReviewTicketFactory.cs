using VenueGo.Data;
using VenueGo.Services;
using VenueGo.Models.Enums;
using VenueGo.Models.Entities;
using VenueGo.Helpers;

namespace VenueGo.Models.ReviewModels
{
    // 一個實作、兩個門：報到系統看到 IVisitReviewTicketFactory，
    // 訂單／付款系統看到 IBookingReviewTicketFactory，各自只看得到自己該叫的方法。
    public class ReviewTicketFactory(dbVenueContext db)
        : IVisitReviewTicketFactory, IBookingReviewTicketFactory
    {
        private readonly dbVenueContext _db = db;

        // 建立現場評論資格憑證
        public bool CreateReviewPerVisit(string? token)
        {
            if (token == null) return false;

            if (_db.ReviewPerVisits.Any(v => v.Qrtoken == token)) return false; // 防止重複建立

            var entry = _db.EntryTickets.FirstOrDefault(t => t.Qrtoken == token);
            if (entry == null) return false;
            if (entry.Status != (byte)EntryTicketStatus.Used) return false; // 要先把票券狀態改為 Used 再呼叫

            var order = _db.Orders.FirstOrDefault(o => o.OrderId == entry.OrderId);
            if (order == null) return false;

            var reservation = _db.Reservations.FirstOrDefault(s => s.ReservationId == order.ReservationId);
            if (reservation == null) return false;

            var venue = _db.Venues.FirstOrDefault(n => n.VenueId == reservation.VenueId);
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
                CreatedAt = DateTime.Now,
                ExpiredAt = DateTime.Now.AddDays(CDictionary.DAY_評論資格期限天數)
            };

            _db.ReviewPerVisits.Add(newVisit);
            _db.SaveChanges();

            return true;
        }

        // 校正現場評論憑證之實際離場時間
        public bool RecordVisitEndTime(int? ticketId)
        {
            if (ticketId == null) return false;

            var entry = _db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
            if (entry == null) return false;

            var reservation = (from s in _db.Reservations
                              join o in _db.Orders on s.ReservationId equals o.ReservationId
                              where o.OrderId == entry.OrderId
                              select s).FirstOrDefault();
            if (reservation == null) return false;

            var latestLog = _db.CheckInLogs
                 .Where(c => c.TicketId == ticketId)
                 .OrderByDescending(c => c.ActionTime)
                 .FirstOrDefault();
            if (latestLog == null) return false;

            var rentStartTime = reservation.BookingDate.ToDateTime(reservation.StartTime);
            if (latestLog.ActionTime < rentStartTime) return false; // 離場時間不可能早於租借開始時間

            var visitTicket = _db.ReviewPerVisits.FirstOrDefault(v => v.Qrtoken == entry.Qrtoken);
            if (visitTicket == null) return false;

            visitTicket.ActualEndTime = latestLog.ActionTime;
            _db.SaveChanges();

            return true;
        }

        // 建立預約評論資格憑證
        public bool CreateReviewPerBooking(int? orderId)
        {
            if (orderId == null) return false;

            if (_db.ReviewPerBookings.Any(b => b.OrderId == orderId)) return false; // 防止重複建立

            var book = (from p in _db.Payments
                        join o in _db.Orders on p.OrderId equals o.OrderId
                        where o.OrderId == orderId && p.PaidAt != null // 有付款紀錄才建立
                        select new { p, o }).FirstOrDefault();
            if (book == null) return false;
            var order = book.o; 
            var payment = book.p;

            var newBooking = new ReviewPerBooking
            {
                UserId = order.UserId,
                OrderId = order.OrderId,
                PaymentMethod = payment.PaymentMethod,
                CreatedAt = DateTime.Now,
                ExpiredAt = DateTime.Now.AddDays(CDictionary.DAY_評論資格期限天數)
            };

            _db.ReviewPerBookings.Add(newBooking);
            _db.SaveChanges();

            return true;
        }
    }
}
