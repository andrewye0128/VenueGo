namespace VenueGo.Dtos.BookingDtos
{
    /// <summary>
    /// 試算結果（GET /api/booking/quote）：填寫資料頁一打開就呼叫。
    /// <para>
    /// 欄位要跟前端 src/api/bookingApi.js 的 getBookingQuote 註解一致（填寫資料頁使用）。
    /// </para>
    /// <para>
    /// 金額只是給客人看的；建立預約時後端會重新查證時段、重新計價，不採用這裡的金額。
    /// </para>
    /// </summary>
    public class BookingQuoteDto
    {
        public int VenueId { get; init; }

        public string VenueName { get; init; } = string.Empty;

        public int SportTypeId { get; init; }

        public string SportName { get; init; } = string.Empty;

        /// <summary>可容納人數。資料庫允許 NULL，null 時前端人數上限用 20。</summary>
        public int? Capacity { get; init; }

        /// <summary>使用日期，JSON 會是 "2026-10-20"。</summary>
        public DateOnly BookingDate { get; init; }

        /// <summary>第一格的開始時間，"17:00"（只到分鐘，請用 TimeSlotStatus.FormValue 的格式）。</summary>
        public string StartTime { get; init; } = string.Empty;

        /// <summary>最後一格的結束時間，"19:00"。</summary>
        public string EndTime { get; init; } = string.Empty;

        /// <summary>每一格的價格，依時間排序。</summary>
        public IReadOnlyList<QuoteSlotDto> Slots { get; init; } = [];

        /// <summary>總金額（各格單價加總，由 PricingResult.TotalAmount 提供）。</summary>
        public int TotalAmount { get; init; }

        /// <summary>
        /// 付款方式與期限（線上、現場各一筆）。能不能選、期限由後端依 ReservationRulesOptions 計算，
        /// 前端只負責顯示，付款規則不在前端再寫一份。
        /// </summary>
        public IReadOnlyList<PaymentOptionDto> PaymentOptions { get; init; } = [];

        /// <summary>目前的租借條款版本（appsettings.json 的 ReservationRules.TermsVersion）。建立預約時要原樣送回。</summary>
        public string TermsVersion { get; init; } = string.Empty;
    }

    /// <summary>試算的一格時段。</summary>
    public class QuoteSlotDto
    {
        /// <summary>開始時間 "17:00"。</summary>
        public string SlotTime { get; init; } = string.Empty;

        /// <summary>單價。能走到試算成功，代表每一格都有價格，所以不是 null。</summary>
        public int UnitPrice { get; init; }

        /// <summary>是否為尖峰時段。</summary>
        public bool IsPeak { get; init; }
    }

    /// <summary>一種付款方式能不能選、期限是什麼時候。</summary>
    public class PaymentOptionDto
    {
        /// <summary>付款方式（PaymentMethod 列舉的數字）：0 現場、1 線上。</summary>
        public byte PaymentMethod { get; init; }

        /// <summary>這筆預約能不能選這種付款方式。</summary>
        public bool IsAvailable { get; init; }

        /// <summary>
        /// 「如果現在送出」的付款期限，JSON 會是 "2026-10-14T15:30:00"（不帶時區）。
        /// 不能選時是 null。只是參考，建立預約時會用當下的時間重新計算。
        /// </summary>
        public DateTime? DueAt { get; init; }

        /// <summary>不能選的原因，例如「距離使用日不到 3 天，只能線上付款」；能選時是 null。</summary>
        public string? UnavailableReason { get; init; }
    }
}
