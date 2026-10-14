namespace VenueGo.Dtos.ReservationDtos
{
    /// <summary>
    /// 建立預約成功的回傳（POST /api/reservations，201）。
    /// <para>
    /// 前端的預約完成頁直接顯示這份資料（不另外再查），
    /// 所以除了編號，場地、日期、時段、金額、付款期限都要一起回傳。
    /// 欄位要跟前端 src/mocks/bookingQuote.js 的 buildMockReservation 一致。
    /// </para>
    /// </summary>
    public class CreateReservationResultDto
    {
        public int ReservationId { get; init; }

        /// <summary>訂單編號，例如 VG20261014003。</summary>
        public string OrderNo { get; init; } = string.Empty;

        public string VenueName { get; init; } = string.Empty;

        public string SportName { get; init; } = string.Empty;

        /// <summary>使用日期，JSON 會是 "2026-10-20"。</summary>
        public DateOnly BookingDate { get; init; }

        /// <summary>開始時間 "17:00"。</summary>
        public string StartTime { get; init; } = string.Empty;

        /// <summary>結束時間 "19:00"。</summary>
        public string EndTime { get; init; } = string.Empty;

        public int PersonAmount { get; init; }

        public int TotalAmount { get; init; }

        /// <summary>付款方式（PaymentMethod 列舉的數字）：0 現場、1 線上。</summary>
        public byte PaymentMethod { get; init; }

        /// <summary>付款期限，JSON 會是 "2026-10-14T15:30:00"（不帶時區）。</summary>
        public DateTime PaymentDueAt { get; init; }

        /// <summary>預約狀態（ReservationStatus 列舉的數字）；會員自助預約一律是 0（待確認）。</summary>
        public byte ReservationStatus { get; init; }
    }
}
