using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Enums;
using VenueGo.Services.Ticket;
using VenueGo.ViewModels.CheckinViewModels;
using VenueGo.ViewModels.Shared;

namespace VenueGo.Models.CheckinModels
{
    public class CEntryTicketFactory
    {
        private readonly dbVenueContext _db;

        public CEntryTicketFactory(dbVenueContext db)
        {
            _db = db;
        }

        public PagedResult<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status, int page, int pageSize)
        {
            // 組查詢
            IQueryable<EntryTicketListViewModel> query = _db.Database.SqlQuery<EntryTicketListViewModel>($@"
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
                    AND  ({status}  IS NULL OR TicketStatusValue = {status})");

            int totalCount = query.Count();

            int totalPage = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if(page < 1)
            {
                page = 1;
            }
            if(page > totalPage)
            {
                page = totalPage;
            }

            List<EntryTicketListViewModel> list = query
                .OrderBy(x => x.StartTime)
                .ThenBy(x => x.TicketId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            return new PagedResult<EntryTicketListViewModel>
            {
                Items = list,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public void ChangeHolder(EntryTicket ticket, int newUserId)
        {
            ticket.UserId = newUserId;
            ticket.ReceivedUserId = newUserId;
            ticket.Qrtoken = $"TCK-{Guid.NewGuid():N}";
        }

        // 取得目前票券QRtoken, QR驗證重算簽章, 找度到回 null
        public async Task<string?> GetQrtokenAsync(int ticketId)
        {
            return await _db.EntryTickets
                .Where(t => t.TicketId == ticketId)
                .Select(t => t.Qrtoken)
                .FirstOrDefaultAsync();
        }
    }
}
