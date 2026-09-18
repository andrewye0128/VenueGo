using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;

namespace VenueGo.Services
{
    public class EntryTicketService : IEntryTicketService
    {
        private readonly dbVenueContext _db;

        public EntryTicketService(dbVenueContext db)
        {
            _db = db; //建構子注入: 跟訂單Controller一樣和dbVenueContext建立關聯
        }

        public async Task<(string Message, int TicketCount)> CreateForOrderAsync(int orderId)
        {
            if (await _db.EntryTickets.AnyAsync(t => t.OrderId == orderId))
                return (Message: "票卷已在處理中, 請勿重複申請", TicketCount: 0);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return (Message: "找不到訂單", TicketCount: 0);

            int count = order.PersonMount > 0 ? order.PersonMount : 1;

            for (int i = 0; i < count; i++)
            {
                _db.EntryTickets.Add(new EntryTicket
                {
                    OrderId = orderId,
                    Qrtoken = $"TCK-{Guid.NewGuid():N}",
                    Status = (byte)EntryTicketStatus.Valid,
                    CreatedAt = DateTime.Now,
                    UserId = order.UserId
                });
            }
            await _db.SaveChangesAsync();
            return (Message: "成功產生票券", TicketCount: count);
        }



        public async Task<int> CancelByOrderAsync(int orderId)
        {
            var tickets = await _db.EntryTickets
                .Where(t => t.OrderId == orderId && t.Status == (byte)EntryTicketStatus.Valid)
                .ToListAsync();

            if (tickets.Count > 0)
                foreach (var t in tickets)
                t.Status = (byte)EntryTicketStatus.Cancelled;
            else
                return 0;
            await _db.SaveChangesAsync();

            return tickets.Count;
        }
    }
}