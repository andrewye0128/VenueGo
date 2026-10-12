namespace VenueGo.Dtos.BookingDtos
{
    /// <summary>
    /// 某天各場地的時段（GET /api/booking/availability）。
    /// <para>
    /// 欄位要跟前端 src/mocks/bookingAvailability.js 一致。
    /// </para>
    /// </summary>
    public class BookingAvailabilityDto
    {
        /// <summary>查詢的日期，JSON 會是 "2026-10-14"。</summary>
        public DateOnly Date { get; init; }

        /// <summary>可預約的場地（場地與運動都是啟用中）。前端依 SportTypeId 分組顯示。</summary>
        public IReadOnlyList<VenueAvailabilityDto> Venues { get; init; } = [];
    }

    /// <summary>一個場地在某天的時段。</summary>
    public class VenueAvailabilityDto
    {
        public int VenueId { get; init; }

        public int SportTypeId { get; init; }

        public string VenueName { get; init; } = string.Empty;

        /// <summary>可容納人數。資料庫允許 NULL，null 時前端不顯示「可容納 N 人」。</summary>
        public int? Capacity { get; init; }

        /// <summary>場地照片路徑。null 或讀取失敗時，前端顯示運動圖示。</summary>
        public string? PhotoPath { get; init; }

        /// <summary>這天的每一格（含不能選的）。公休日是空陣列。</summary>
        public IReadOnlyList<SlotDto> Slots { get; init; } = [];
    }

    /// <summary>一個時段（一格一小時）。</summary>
    public class SlotDto
    {
        /// <summary>
        /// 開始時間，格式一定要是 "18:00"（只到分鐘）。
        /// 請用 TimeSlotStatus.FormValue，不要用 TimeOnly：
        /// TimeOnly 輸出成 JSON 會是 "18:00:00"，前端的選取規則會對不上。
        /// </summary>
        public string SlotTime { get; init; } = string.Empty;

        /// <summary>時段狀態（SlotAvailability 的數字）：0 可預約、1 已預約、2 維護中、3 已過時間。</summary>
        public byte Availability { get; init; }

        /// <summary>單價。查不到計價規則時是 null，前端會當成不能選、顯示「價格未設定」。</summary>
        public int? UnitPrice { get; init; }

        /// <summary>是否為尖峰時段。</summary>
        public bool IsPeak { get; init; }
    }
}
