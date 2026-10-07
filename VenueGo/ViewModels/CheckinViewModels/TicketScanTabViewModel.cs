using VenueGo.Models.CheckinModels;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.CheckinViewModels
{
    //「報到管理」分頁 >> 手動報到/離場按鈕 + 掃描紀錄
    public class TicketScanTabViewModel : TicketTabViewModelBase
    {
        public List<CCheckInLogWrap> Logs { get; set; } = new List<CCheckInLogWrap>();

        //按鈕要不要能按
        public bool CanCheckIn => !IsPastEndTime && !IsInside
            && (Status == (byte)EntryTicketStatus.Valid || Status == (byte)EntryTicketStatus.Used);

        public bool CanCheckOut => IsInside
            && (Status == (byte)EntryTicketStatus.Used || Status == (byte)EntryTicketStatus.Expired);
    }
}