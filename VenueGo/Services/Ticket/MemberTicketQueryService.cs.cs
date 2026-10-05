using VenueGo.Data;
using VenueGo.Dtos.TicketDtos;
using VenueGo.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace VenueGo.Services.Ticket
{
    public class MemberTicketQueryService : IMemberTicketQueryService
    {

        private readonly dbVenueContext _db;
        private readonly ITimeService _time;

        public MemberTicketQueryService(dbVenueContext db, ITimeService time)
        {
            _db = db;
            _time = time;
        }


        public async Task<List<MemberTicketDto>> GetMyTicketsAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            // 持有人 = EntryTicket.UserId；舊資料是 null 就退回訂購人 (Order.UserId)
            // 撈：我持有的票 + 我訂購但已轉給別人的票；已取消的票在這裡就過濾掉
            var rows = await (from t in _db.EntryTickets
                              join o in _db.Orders on t.OrderId equals o.OrderId
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              join v in _db.Venues on r.VenueId equals v.VenueId
                              join s in _db.SportTypes on v.SportTypeId equals s.SportTypeId
                              where t.Status != (byte)EntryTicketStatus.Cancelled
                                 && ((t.UserId ?? o.UserId) == userId || o.UserId == userId)
                              orderby r.BookingDate descending, r.StartTime descending
                              select new
                              {
                                  t.TicketId,
                                  t.Qrtoken,
                                  t.Status,
                                  HolderId = t.UserId ?? o.UserId,
                                  r.BookingDate,
                                  r.StartTime,
                                  r.EndTime,
                                  v.VenueName,
                                  s.SportName
                              })
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
            var now = _time.Now;

            return rows.Select(x =>
            {
                string status = ToMemberStatus(
                    x.Status, x.HolderId == userId, x.BookingDate.ToDateTime(x.EndTime), now);

                return new MemberTicketDto
                {
                    Id = x.TicketId,
                    VenueName = x.VenueName,
                    SportType = x.SportName,
                    Date = x.BookingDate.ToString("yyyy/MM/dd"),
                    TimeRange = $"{x.StartTime:HH\\:mm}-{x.EndTime:HH\\:mm}",
                    Status = status,
                    QrToken = status == "available" ? x.Qrtoken : null   // 只有可使用的票才給 QR
                };
            }).ToList();
        }
        private static string ToMemberStatus(byte dbStatus, bool heldByMe, DateTime endAt, DateTime now)
        {
            // 票已經不在我手上（我訂購但轉給別人了）
            if (!heldByMe) return "transferred";

            // Used（入場中）與 Completed（已離場）對會員來說都是「已使用」
            if (dbStatus == (byte)EntryTicketStatus.Used
                || dbStatus == (byte)EntryTicketStatus.Completed)
                return "used";

            // 已標記失效，或還是 Valid 但時段已過（排程每 30 分鐘才結算，中間有空窗）
            if (dbStatus == (byte)EntryTicketStatus.Expired || endAt < now)
                return "expired";

            return "available";
        }

    }
}
