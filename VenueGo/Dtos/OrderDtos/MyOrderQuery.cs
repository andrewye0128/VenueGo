namespace VenueGo.Dtos.OrderDtos
{
    /// <summary>
    /// 前台「訂單與付款」列表的查詢條件，對應網址參數：
    /// GET /api/orders?keyword=VG2026&amp;date=2026-10-20&amp;paymentMethod=1&amp;paymentStatus=0&amp;page=1&amp;pageSize=10
    /// <para>
    /// 【屬性名稱就是網址參數名稱】改名時前端 src/api/orderApi.js 也要一起改。
    /// 可為 null 的屬性＝前端選「全部」時不傳，就不篩選。
    /// </para>
    /// </summary>
    public class MyOrderQuery
    {
        /// <summary>關鍵字：場地名稱或訂單編號「包含」（跟「我的預約」同一條規則）。</summary>
        public string? Keyword { get; set; }

        /// <summary>使用日期（Reservations.BookingDate），完全相同才符合。</summary>
        public DateOnly? Date { get; set; }

        /// <summary>付款方式（PaymentMethod 列舉的數字），比對最新一筆付款。</summary>
        public byte? PaymentMethod { get; set; }

        /// <summary>付款狀態（PaymentStatus 列舉的數字），比對最新一筆付款。</summary>
        public byte? PaymentStatus { get; set; }

        /// <summary>第幾頁，從 1 開始。小於 1 時由 Service 當成 1。</summary>
        public int Page { get; set; } = 1;

        /// <summary>每頁幾筆。由 Service 限制在 1～50。</summary>
        public int PageSize { get; set; } = 10;
    }
}
