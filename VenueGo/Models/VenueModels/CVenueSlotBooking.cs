namespace VenueGo.Models.VenueModels
{
    //某個時段目前被哪一筆預約佔用 >> 給VenueUnavailableSlotManage頁面提醒後台人員、關閉時段時取消預約與寄信使用,只讀不寫
    //資料來自 ReservationSlots + Reservations + Users + Venues + Orders,由 CVenueUnavailableSlotFactory.QueryBookingsByVenueAndDate() 產生
    public class CVenueSlotBooking
    {
        public TimeOnly SlotTime { get; set; }           //被佔用的時段起始時間

        public int ReservationId { get; set; }           //連到預約詳細頁(/Reservation/Details/{id})、場館取消時使用

        public int? OrderId { get; set; }                //取消票券(IEntryTicketService.CancelByOrderAsync)使用,查不到訂單時為null

        public string? OrderNo { get; set; }             //訂單編號,理論上每筆預約都有訂單,查不到時為null

        public string MemberName { get; set; } = string.Empty;

        public string MemberEmail { get; set; } = string.Empty;

        public byte ReservationStatus { get; set; }      //對應 Enums.ReservationStatus,轉中文交給 Controller

        public string VenueName { get; set; } = string.Empty;   //通知信顯示場地名稱

        public DateOnly BookingDate { get; set; }        //通知信顯示使用日期

        //整筆預約的起訖時間(一筆預約可能跨多個時段),確認視窗與通知信顯示整筆的使用時間
        //EndTime 是最後一格的結束時間,例如 20:00～22:00 三格 >> StartTime 20:00、EndTime 23:00
        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }
    }
}
