using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class EntryTicketListViewModel
    {
        public int TicketId { get; set; }
        public string Qrtoken { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";
        public byte Status { get; set; }

        public string StatusText => (EntryTicketStatus)Status switch
        {
            EntryTicketStatus.Valid => "有效",
            EntryTicketStatus.Used => "已使用",
            EntryTicketStatus.Expired => "已逾期",
            EntryTicketStatus.Cancelled => "已取消",
            _ => $"未知({Status})"
        };

        public bool IsCheckedIn => (EntryTicketStatus)Status == EntryTicketStatus.Used;
    }
}
