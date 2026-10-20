namespace VenueGo.Dtos.ReservationDtos
{
    /// <summary>
    /// 前台「我的預約」列表的查詢條件，對應網址參數：
    /// GET /api/reservations?keyword=羽球&amp;date=2026-10-08&amp;reservationStatus=0&amp;page=1&amp;pageSize=10
    /// <para>
    /// 【屬性名稱就是網址參數名稱】ASP.NET Core 會自動把網址參數填進同名屬性（不分大小寫），
    /// 改名時前端 src/api/reservationApi.js 也要一起改。
    /// </para>
    /// <para>
    /// 【可為 null 的屬性（加 ?）＝不傳就不篩選】
    /// 前端選「全部」時不會送該參數，這裡就是 null。
    /// </para>
    /// </summary>
    public class MyReservationQuery
    {
        /// <summary>
        /// 關鍵字：場地名稱或訂單編號「包含」。
        /// 例如 "羽球"、"VG20261015001"。
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>預約日期（網址格式 2026-10-08），完全相同才符合。</summary>
        public DateOnly? Date { get; set; }

        /// <summary>預約狀態（對應 ReservationStatus 列舉的數字）。</summary>
        public byte? ReservationStatus { get; set; }

        /// <summary>付款狀態（對應 PaymentStatus 列舉的數字）。</summary>
        public byte? PaymentStatus { get; set; }

        /// <summary>第幾頁，從 1 開始。小於 1 時由 Service 當成 1。</summary>
        public int Page { get; set; } = 1;

        /// <summary>每頁幾筆。由 Service 限制在 1～50，避免一次撈出全部資料。</summary>
        public int PageSize { get; set; } = 10;
    }
}
