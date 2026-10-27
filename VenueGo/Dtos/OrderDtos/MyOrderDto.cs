namespace VenueGo.Dtos.OrderDtos
{
    /// <summary>
    /// 前台「訂單與付款」列表的一筆資料。
    /// 欄位要跟前端 src/api/orderApi.js 的 getMyOrders 註解一致。
    /// <para>
    /// 【資料來源】Orders + Reservations + Venues + SportTypes + Payments（取最新一筆）。
    /// </para>
    /// <para>
    /// 【付款欄位可為 null】正常情況下建立預約時一定會有一筆付款；
    /// 萬一沒有，訂單還是要出現在列表上，所以付款欄位用 ? 。
    /// </para>
    /// </summary>
    public class MyOrderDto
    {
        // ── 訂單（Orders）──

        public int OrderId { get; init; }

        public string OrderNo { get; init; } = string.Empty;

        public int TotalAmount { get; init; }

        /// <summary>下單時間，列表依這個欄位新到舊排序。</summary>
        public DateTime OrderCreatedAt { get; init; }

        // ── 預約（Reservations）──

        /// <summary>前端不顯示，保留給之後跳轉或除錯使用。</summary>
        public int ReservationId { get; init; }

        public DateOnly BookingDate { get; init; }

        public TimeOnly StartTime { get; init; }

        public TimeOnly EndTime { get; init; }

        /// <summary>預約狀態（ReservationStatus 列舉的數字）</summary>
        public byte ReservationStatus { get; init; }

        // ── 場地、運動（Venues、SportTypes）──

        public string VenueName { get; init; } = string.Empty;

        public string SportName { get; init; } = string.Empty;

        // ── 付款（Payments，取最新一筆）──

        /// <summary>付款方式（PaymentMethod 列舉的數字）：0 現場、1 線上。</summary>
        public byte? PaymentMethod { get; init; }

        /// <summary>付款狀態（PaymentStatus 列舉的數字）。</summary>
        public byte? PaymentStatus { get; init; }

        public DateTime? PaymentDueAt { get; init; }

        public DateTime? PaidAt { get; init; }

        /// <summary>
        /// 最晚可以開始付款的時間（付款期限往前幾分鐘，分鐘數。
        /// <para>
        /// 只有「線上付款、未付款或付款失敗、預約待確認」才有
        /// 已經超過也照樣回傳，前端用網站時間比較，倒數到 0 時「付款」按鈕自己消失。
        /// </para>
        /// <para>
        /// 用 set 不用 init：要讀設定值計算，SQL 算不了，由 S
        /// </para>
        /// </summary>
        public DateTime? PaymentStartDeadline { get; set; }
    }
}