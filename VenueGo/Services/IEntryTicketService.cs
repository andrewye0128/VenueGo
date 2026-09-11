namespace VenueGo.Services
{
    public interface IEntryTicketService
    {
        Task<(string Message, int TicketCount)> CreateForOrderAsync(int orderId); // 建立 EntryTicket，並回傳建立的 EntryTicket 數量
        Task<int> CancelByOrderAsync(int orderId); // 依據 OrderId 取消 EntryTicket，並回傳取消的 EntryTicket 數量
    }
}
