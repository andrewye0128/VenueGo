namespace VenueGo.Dtos.BookingDtos
{
    /// <summary>
    /// 某一天還剩幾格（GET /api/booking/availability/summary）。
    /// 前台預約頁的日期列使用：「剩 12」「已額滿」。
    /// </summary>
    public class DayAvailabilitySummaryDto
    {
        /// <summary>日期，JSON 會是 "2026-10-14"。</summary>
        public DateOnly Date { get; init; }

        /// <summary>這一天（時段篩選範圍內）所有場地加總的可預約格數。0 表示已額滿或公休。</summary>
        public int AvailableCount { get; init; }
    }
}
