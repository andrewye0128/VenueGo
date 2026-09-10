using System.ComponentModel;

namespace VenueGo.ViewModels
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
        public byte ReservationStatus { get; set; }

        [DisplayName("付款狀態")]
        public byte? PaymentStatus { get; set; }

        public string PaymentStatusText => PaymentStatus switch
        {
            null => "尚未成立訂單",
            0 => "未付款",
            1 => "已付款",
            2 => "付款失敗",
            3 => "付款取消",
            4 => "退款處理中",
            5 => "已退款",
            _ => $"未知({PaymentStatus})"
        };
    }
}
