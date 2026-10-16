using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Services.Ticket;
using VenueGo.ViewModels.CheckinViewModels;

namespace VenueGo.Models.CheckinModels
{
    public class CEntryTicketFactory
    {
        private readonly dbVenueContext _db;

        public CEntryTicketFactory(dbVenueContext db)
        {
            _db = db;
        }

        public List<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status)
        {
            List<EntryTicketListViewModel> list = _db.Database.SqlQuery<EntryTicketListViewModel>($@"
                SELECT TicketId,
                    TicketQRToken     AS Qrtoken,
                    UserName,
                    UserPhone         AS Phone,
                    VenueName,
                    BookingDate,
                    StartTime,
                    EndTime,
                    TicketStatusValue AS Status
                FROM   dbo.v_BookingTicketInfo
                WHERE  TicketId IS NOT NULL
                    AND  BookingDate = {date}
                    AND  ({keyword} IS NULL OR UserName      LIKE '%' + {keyword} + '%'
                                  OR UserPhone     LIKE '%' + {keyword} + '%'
                                  OR TicketQRToken LIKE '%' + {keyword} + '%')
                    AND  ({venueId} IS NULL OR VenueId = {venueId})
                    AND  ({status}  IS NULL OR TicketStatusValue = {status})")
                .OrderBy(x => x.StartTime)
                .ToList();

            // 臨時測試用之後改檢視表
            List<int> test = list.Select(t => t.TicketId).ToList();

            var holders = (from t in _db.EntryTickets.AsNoTracking()
                           join u in _db.Users on t.UserId equals u.UserId
                           where test.Contains(t.TicketId)
                           select new { t.TicketId, u.Name })
                         .ToDictionary(x => x.TicketId, x => x.Name);

            foreach (var item in list)
            {
                item.Holder = holders.ContainsKey(item.TicketId) ? holders[item.TicketId] : item.UserName;
            }

            return list;
        }

        public void ChangeHolder(EntryTicket ticket, int newUserId)
        {
            ticket.UserId = newUserId;
            ticket.ReceivedUserId = newUserId;
            ticket.Qrtoken = $"TCK-{Guid.NewGuid():N}";
        }
    }
}
