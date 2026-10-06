using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class TicketTabViewModelBase
    {
        public int TicketId { get; set; }
        public string Qrtoken { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public byte Status { get; set; }

        public string VenueName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";

        //以下兩個由 Factory 查詢進出紀錄後填入
        //最後一筆「有效」的進出紀錄是入場 → 人在場內
        public bool IsInside { get; set; }
        //有沒有任何一筆有效的入場紀錄
        public bool HasValidCheckIn { get; set; }

        //是否超過預約結束時間
        public bool IsPastEndTime => DateTime.Now > BookingDate.ToDateTime(EndTime);

        public string StatusText => ((EntryTicketStatus)Status).GetDisplayName();

        //失效原因(只有已失效才有值)
        public string? ExpiredReasonText
        {
            get
            {
                if (Status != (byte)EntryTicketStatus.Expired) return null;
                if (HasValidCheckIn) return "票券超時失效";
                return "未使用票券";
            }
        }

        //狀態標籤的 Bootstrap 顏色
        public string StatusBadgeClass
        {
            get
            {
                EntryTicketStatus status = (EntryTicketStatus)Status;
                if (status == EntryTicketStatus.Valid) return "bg-success";
                if (status == EntryTicketStatus.Used) return "bg-secondary";
                if (status == EntryTicketStatus.Expired) return "bg-warning text-dark";
                if (status == EntryTicketStatus.Cancelled) return "bg-danger";
                if (status == EntryTicketStatus.Completed) return "bg-info text-dark";
                return "bg-dark";
            }
        }
    }
}
