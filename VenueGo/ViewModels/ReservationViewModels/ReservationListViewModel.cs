using System.ComponentModel;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.ReservationViewModels
{
    public class ReservationListViewModel
    {
        [DisplayName("預約編號")]
        public int ReservationId { get; set; }

        [DisplayName("會員名稱")]
        public string UserName { get; set; } = string.Empty;

        [DisplayName("場地名稱")]
        public string VenueName { get; set; } = string.Empty;

        [DisplayName("預約日期")]
        public DateOnly BookingDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        [DisplayName("使用時段")]
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";

        [DisplayName("預約狀態")]
        public ReservationStatus ReservationStatus { get; set; }

        public string ReservationStatusText => ReservationStatus.GetDisplayName();

        [DisplayName("付款狀態")]
        public PaymentStatus? PaymentStatus { get; set; }

        public string PaymentStatusText => PaymentStatus?.GetDisplayName() ?? "尚未成立訂單";
    }
}
