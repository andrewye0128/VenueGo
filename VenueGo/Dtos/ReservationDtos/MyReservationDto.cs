namespace VenueGo.Dtos.ReservationDtos
{
    /// <summary>
    /// 前台「我的預約」列表的一筆資料。
    /// 送到前端後屬性名會變成小寫開頭（reservationId、venueName…），
    /// 要跟前端假資料 src/mocks/myReservations.js 的欄位一致。
    /// <para>
    /// 【資料來源】Reservations + Venues + SportTypes + Orders + Payments 五張表 join。
    /// </para>
    /// <para>
    /// 【可為 null（加 ?）的欄位】Orders.ReservationId 允許 null，代表一筆預約可能沒有訂單或付款紀錄，
    /// 這時人數、金額、付款相關欄位都是 null，前端要能處理。
    /// </para>
    /// <para>
    /// 【狀態用數字（byte）】前端用 constants/statuses.js 對照成文字、顏色、圖示，
    /// 所以這裡不回傳中文，避免前後端各寫一份中文、改的時候對不起來。
    /// </para>
    /// </summary>
    public class MyReservationDto
    {
        // ── 預約（Reservations）──

        public int ReservationId { get; init; }

        /// <summary>預約日期，JSON 會是 "2026-10-08"。</summary>
        public DateOnly BookingDate { get; init; }

        /// <summary>開始時間，JSON 會是 "18:00:00"（前端只顯示到分鐘）。</summary>
        public TimeOnly StartTime { get; init; }

        public TimeOnly EndTime { get; init; }

        /// <summary>預約狀態（ReservationStatus 列舉的數字）。</summary>
        public byte ReservationStatus { get; init; }

        /// <summary>建立時間，JSON 會是 "2026-10-05T14:30:00"。</summary>
        public DateTime ReservedAt { get; init; }

        public DateTime? CancelledAt { get; init; }

        public string? CancelReason { get; init; }

        // ── 場地、運動（Venues、SportTypes）──

        public string VenueName { get; init; } = string.Empty;

        public string SportName { get; init; } = string.Empty;

        // ── 訂單（Orders）：沒有訂單時是 null ──

        /// <summary>預約人數（Orders.PersonMount）。</summary>
        public int? PersonCount { get; init; }

        public int? TotalAmount { get; init; }

        // ── 付款（Payments，取最新一筆）：沒有付款紀錄時是 null ──

        /// <summary>付款狀態（PaymentStatus 列舉的數字）。</summary>
        public byte? PaymentStatus { get; init; }

        public DateTime? PaymentDueAt { get; init; }
    }
}
