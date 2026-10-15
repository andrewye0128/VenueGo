using VenueGo.Enums;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 寫入一筆預約需要的所有值：交給 IReservationCreationService.WriteAsync 寫進資料庫。
    /// <para>
    /// 【為何需要這個類別】寫入的程式（交易、5 張表、搶位、訂單編號撞號重試）
    /// 原本直接讀後台的 Session 草稿（ReservationDraft），所以只能給後台用。
    /// 改成讀這張「單子」之後，後台代客建立、前台會員預約都能共用同一段寫入程式。
    /// 各種狀態、付款方式、期限由呼叫的人決定（後台、前台規則不同），寫入的程式只負責照單寫入。
    /// </para>
    /// <para>
    /// 【金額】一律放在 <see cref="Pricing"/>，必須是伺服器端查證過的時段算出來的，
    /// 不可以使用前端送來的金額。
    /// </para>
    /// </summary>
    public class ReservationWriteModel
    {
        // ── 誰、哪裡、什麼時候 ─────────────────────────────

        /// <summary>來打球的會員（Reservations.UserId、Orders.UserId）。</summary>
        public required int UserId { get; init; }

        /// <summary>場地。</summary>
        public required int VenueId { get; init; }

        /// <summary>使用日期。</summary>
        public required DateOnly BookingDate { get; init; }

        /// <summary>
        /// 計價結果：每一格的時段、單價，以及開始／結束時間、總金額。
        /// 必須來自 ISlotSelectionValidator 查證過的時段＋IReservationPricingService。
        /// </summary>
        public required PricingResult Pricing { get; init; }

        /// <summary>這次預約的時段起始時間（佔位衝突時，用來重新查是哪幾格被搶走）。</summary>
        public IReadOnlyList<TimeOnly> SlotTimes => Pricing.Details.Select(d => d.SlotTime).ToList();

        // ── 訂單內容 ─────────────────────────────────────

        /// <summary>使用人數（Orders.PersonMount）。</summary>
        public required int PersonAmount { get; init; }

        /// <summary>發票類型。</summary>
        public required InvoiceType InvoiceType { get; init; }

        /// <summary>手機條碼載具；紙本發票時一定要是 null（呼叫的人負責）。</summary>
        public string? CarrierNo { get; init; }

        // ── 付款 ────────────────────────────────────────

        /// <summary>付款方式（現場／線上）。</summary>
        public required PaymentMethod PaymentMethod { get; init; }

        /// <summary>付款通道（櫃台／信用卡／LINE Pay）。</summary>
        public required PaymentChannel PaymentChannel { get; init; }

        /// <summary>付款期限（Payments.PaymentDueAt）。</summary>
        public required DateTime PaymentDueAt { get; init; }

        /// <summary>已付款的時間；還沒付款是 null（Payments.PaidAt）。</summary>
        public DateTime? PaidAt { get; init; }

        // ── 狀態 ────────────────────────────────────────

        /// <summary>預約狀態（待確認／已確認）。</summary>
        public required ReservationStatus ReservationStatus { get; init; }

        /// <summary>訂單狀態（待付款／已付款）。</summary>
        public required OrderStatus OrderStatus { get; init; }

        /// <summary>付款狀態（未付款／已付款）。</summary>
        public required PaymentStatus PaymentStatus { get; init; }

        // ── 來源、操作者、條款 ────────────────────────────

        /// <summary>預約來源（後台代客／會員網頁）。</summary>
        public required ReservationSource Source { get; init; }

        /// <summary>
        /// 操作的人：寫進 Reservations.CreatedBy 與 AuditLogs.UserId。
        /// 後台是管理員的 UserId；前台是會員本人的 UserId。
        /// </summary>
        public required int OperatorUserId { get; init; }

        /// <summary>客人同意的條款版本（Reservations.TermsVersion）。</summary>
        public required string TermsVersion { get; init; }

        /// <summary>
        /// 這次寫入的「現在時間」：ReservedAt、TermsAcceptedAt、OrderCreatedAt、Payments.CreatedAt 都用它。
        /// <para>由呼叫的人傳進來（請用 ITimeService.Now），同一筆預約的時間才會完全一致。</para>
        /// </summary>
        public required DateTime Now { get; init; }
    }
}
