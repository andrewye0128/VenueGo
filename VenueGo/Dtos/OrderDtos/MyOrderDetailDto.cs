namespace VenueGo.Dtos.OrderDtos
{
    /// <summary>
    /// 前台訂單明細（GET /api/orders/{orderId}）＝列表的全部欄位，再加上人數、發票、費用明細、付款紀錄。
    /// <para>
    /// 用繼承：JSON 是平的一層（orderNo、personCount、lines… 都在同一層），
    /// 前端明細視窗讀到之前先用列表那一筆顯示，欄位名稱才會一樣。
    /// </para>
    /// </summary>
    public class MyOrderDetailDto : MyOrderDto
    {
        /// <summary>預約人數（Orders.PersonMount）。</summary>
        public int PersonCount { get; init; }

        /// <summary>發票類型（InvoiceType 列舉的數字）。</summary>
        public byte InvoiceType { get; init; }

        /// <summary>手機條碼載具，例如 /AB12345；沒有時是 null。</summary>
        public string? CarrierNo { get; init; }

        /// <summary>費用明細，一格一列，依時間排序。格式跟試算 API 的 slots 一樣，前端共用 BookingPriceTable。</summary>
        public IReadOnlyList<MyOrderLineDto> Lines { get; init; } = [];

        /// <summary>全部的付款紀錄，新到舊。綠界付款失敗再付一次會有多筆。</summary>
        public IReadOnlyList<MyOrderPaymentDto> Payments { get; init; } = [];
    }

    /// <summary>費用明細的一格。</summary>
    public class MyOrderLineDto
    {
        /// <summary>開始時間 "17:00"（只到分鐘，跟試算 API 一樣）。</summary>
        public string SlotTime { get; init; } = string.Empty;

        /// <summary>下單當下的單價（成交快照，日後調價不會變）。</summary>
        public int UnitPrice { get; init; }

        /// <summary>是否為尖峰，只供顯示（PeakSlotRules）。</summary>
        public bool IsPeak { get; init; }
    }

    /// <summary>
    /// 一筆付款紀錄。
    /// 不回傳 TransactionNo（金流交易編號）：那是對帳用的內部資料，客人用不到。
    /// </summary>
    public class MyOrderPaymentDto
    {
        public byte PaymentMethod { get; init; }

        /// <summary>付款通道（PaymentChannel 列舉的數字）：0 現場櫃台、1 信用卡、2 LINE Pay。</summary>
        public byte PaymentChannel { get; init; }

        public byte PaymentStatus { get; init; }

        public int Amount { get; init; }

        public DateTime PaymentDueAt { get; init; }

        public DateTime? PaidAt { get; init; }

        public DateTime CreatedAt { get; init; }
    }
}