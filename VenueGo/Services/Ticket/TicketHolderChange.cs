using VenueGo.Models.Entities;

namespace VenueGo.Services.Ticket
{
    public class TicketHolderChange
    {
        // 前後台能共用, 更換持有人/加入評論資格會員/更換Qrcode
        public static void Apply(EntryTicket ticket, int newUserId)
        {
            ticket.UserId = newUserId;           
            ticket.ReceivedUserId = newUserId;  
            ticket.Qrtoken = $"TCK-{Guid.NewGuid():N}"; 
        }
    }
}
