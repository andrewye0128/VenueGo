namespace VenueGo.Models.VenueModels
{
    //某個時段目前被哪一筆預約佔用 >> 給VenueUnavailableSlotManage頁面提醒後台人員,只讀不寫
    //資料來自 ReservationSlots + Reservations + Users + Orders,由 CVenueUnavailableSlotFactory.QueryBookingsByVenueAndDate() 產生
    public class CVenueSlotBooking
    {
        public TimeOnly SlotTime { get; set; }           //被佔用的時段起始時間

        public int ReservationId { get; set; }           //連到預約詳細頁(/Reservation/Details/{id})用

        public string? OrderNo { get; set; }             //訂單編號,理論上每筆預約都有訂單,查不到時為null

        public string MemberName { get; set; } = string.Empty;

        public string MemberEmail { get; set; } = string.Empty;

        public byte ReservationStatus { get; set; }      //對應 Enums.ReservationStatus,轉中文交給 Controller
    }
}
