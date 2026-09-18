using VenueGo.Data;
using VenueGo.Services;
using VenueGo.Models.Enums;
using VenueGo.Models.Entities;
using VenueGo.Helpers;

namespace VenueGo.Models.OperationModels
{
    public class ReviewTicketFactory(dbVenueContext db, ICurrentUser currentUser)
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUser _currentUser = currentUser;

        public void createReviewPerVisit(string? token)
        {
            if (token == null) return;

            var entry = _db.EntryTickets.FirstOrDefault(t => t.Qrtoken == token);
            if (entry == null) return;
            if (entry.Status != (byte)EntryTicketStatus.Used) return;

            var order = _db.Orders.FirstOrDefault(o => o.OrderId == entry.OrderId);
            if (order == null) return;

            var reservation = _db.Reservations.FirstOrDefault(s => s.ReservationId == order.ReservationId);
            if (reservation == null) return;

            var venue = _db.Venues.FirstOrDefault(n => n.VenueId == reservation.VenueId);
            if (venue == null) return;

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
        }

        public void recordVisitEndTime(int? ticketId, DateTime? checkoutTime)
        {
            if (ticketId == null || checkoutTime == null) return;

            var entry = _db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
            if (entry == null) return;

            var lastLog = _db.CheckInLogs.LastOrDefault(c => c.TicketId == ticketId);
            if (lastLog == null) return;
            if (lastLog.ActionTime != checkoutTime) return;

            var visitTicket = _db.ReviewPerVisits.FirstOrDefault(v => v.Qrtoken == entry.Qrtoken);
            if (visitTicket == null) return;

            visitTicket.ActualEndTime = checkoutTime;
            _db.SaveChanges();
        }

        public void createReviewPerBooking(int? id)
        {
            if (id == null) return;

            var reservation = _db.Reservations.FirstOrDefault(s => s.ReservationId == id);
            if (reservation == null) return;

            if (_currentUser.MemberId != reservation.UserId) return;

            var order = _db.Orders.FirstOrDefault(o => o.ReservationId == id);
            if(order == null) return;

            var pay = _db.Payments.FirstOrDefault(p => p.OrderId == order.OrderId);
            if (pay == null) return;

            var newBooking = new ReviewPerBooking
            {
                UserId = reservation.UserId,
                OrderId = order.OrderId,
                PaymentMethod = pay.PaymentMethod,
                CreatedAt = DateTime.Now,
                ExpiredAt = DateTime.Now.AddDays(CDictionary.DAY_評論資格期限天數)
            };

            _db.ReviewPerBookings.Add(newBooking);
            _db.SaveChanges();
        }
    }
}
