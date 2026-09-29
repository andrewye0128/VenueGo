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
            EntryTicketStatus.Expired => "已失效",
            EntryTicketStatus.Cancelled => "已取消",
            EntryTicketStatus.Completed => "已完成",
            _ => $"未知({Status})"
        };

        public string VenueName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";

        public List<CheckInLogViewModel> Logs { get; set; } = new();
        // 是否超過預約時間
        public bool IsPastEndTime => DateTime.Now > BookingDate.ToDateTime(EndTime);

        // 最後一筆「有效」的進出紀錄是入場 → 人在場內
        public bool IsInside => Logs
            .Where(l => l.IsValid && (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
            .Select(l => l.Action == (byte)CheckInAction.CheckIn)
            .FirstOrDefault();

        public bool HasValidCheckIn => Logs.Any(l => l.IsValid && l.Action == (byte)CheckInAction.CheckIn);

        // 失效原因（只有已失效才有值）
        public string? ExpiredReasonText =>
            (EntryTicketStatus)Status == EntryTicketStatus.Expired
                ? (HasValidCheckIn ? "超時失效" : "未使用失效")
                : null;

        //前端按鈕顯示用途
        public bool CanCheckIn => !IsPastEndTime && !IsInside
                            && ((EntryTicketStatus)Status is EntryTicketStatus.Valid or EntryTicketStatus.Used);
        public bool CanCheckOut => IsInside
                                   && ((EntryTicketStatus)Status is EntryTicketStatus.Used or EntryTicketStatus.Expired);
        public bool CanCancel => !IsPastEndTime && (EntryTicketStatus)Status == EntryTicketStatus.Valid;
        public bool CanInvalidate => CanCancel;
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
            CheckInAction.ManualExpire => "人工轉失效",
            _ => $"未知({Action})"
        };
    }
}
