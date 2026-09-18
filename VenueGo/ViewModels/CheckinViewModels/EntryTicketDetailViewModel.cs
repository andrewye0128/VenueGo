using VenueGo.Models.Enums;
using VenueGo.Models.Entities;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class EntryTicketDetailViewModel
    {
        public int TicketId { get; set; }
        public string Qrtoken { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusText => (EntryTicketStatus)Status switch
        {
            EntryTicketStatus.Valid => "有效",
            EntryTicketStatus.Used => "已使用",
            EntryTicketStatus.Expired => "已逾期",
            EntryTicketStatus.Cancelled => "已取消",
            _ => $"未知({Status})"
        };

        public string VenueName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";

        public List<CheckInLogViewModel> Logs { get; set; } = new();
    }

    public class CheckInLogViewModel
    {
        public byte Action { get; set; }
        public DateTime ActionTime { get; set; }
        public bool IsManualOverride { get; set; }
        public bool IsValid { get; set; }
        public int? OperatorId { get; set; }

        public string ActionText => (CheckInAction)Action switch
        {
            CheckInAction.CheckIn => "入場",
            CheckInAction.CheckOut => "離場",
            CheckInAction.ManualCancel => "人工取消",
            CheckInAction.ManualExpire => "人工轉逾期",
            _ => $"未知({Action})"
        };
    }
}

