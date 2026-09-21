using System;
using System.Collections.Generic;
using System.Linq;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 計價結果。
    /// <para>
    /// 【單一金額來源】專案中有三個地方存金額：
    /// OrdersDetails.Subtotal（每格多少）、Orders.TotalAmount（整張訂單成交多少）、
    /// Payments.Amount（這筆付款收多少）。
    /// 三者必須一致，唯一的防範方式就是讓它們全部來自同一次計價。
    /// 因此 <see cref="TotalAmount"/> 是由 <see cref="Details"/> 加總得出，
    /// 而不是另外計算一次。呼叫端不要自己再算總額。
    /// </para>
    /// </summary>
    public class PricingResult
    {
        /// <summary>明細，一格時段一列，依時間排序。</summary>
        public IReadOnlyList<OrderDetailLine> Details { get; init; }
            = Array.Empty<OrderDetailLine>();

        /// <summary>
        /// 尚未設定價格的時段。
        /// SportTypePriceRules 查無該運動類型的啟用規則時會落在這裡。
        /// </summary>
        public IReadOnlyList<TimeOnly> SlotsWithoutPrice { get; init; }
            = Array.Empty<TimeOnly>();

        /// <summary>總金額。由明細加總得出，不另外計算。</summary>
        public int TotalAmount => Details.Sum(d => d.Subtotal);

        /// <summary>時段數（小時數）。</summary>
        public int SlotCount => Details.Count;

        /// <summary>
        /// 計價是否完整。有任何時段查不到價格時為 false，
        /// 此時不可建立預約，否則會產生金額為 0 的訂單。
        /// </summary>
        public bool IsComplete => Details.Count > 0 && SlotsWithoutPrice.Count == 0;

        /// <summary>總金額顯示文字。</summary>
        public string TotalAmountText => $"NT$ {TotalAmount:N0}";

        /// <summary>整段時間的起始時間。無明細時為 null。</summary>
        public TimeOnly? StartTime => Details.Count == 0
            ? null
            : Details.Min(d => d.SlotTime);

        /// <summary>
        /// 整段時間的結束時間，寫入 Reservations.EndTime。
        /// 最後一格的起始時間加一小時。
        /// </summary>
        public TimeOnly? EndTime => Details.Count == 0
            ? null
            : Details.Max(d => d.SlotTime).AddHours(1);

        /// <summary>
        /// 依單價分組後的摘要，供步驟 5 顯示「離峰 2 小時 800／尖峰 2 小時 1000」。
        /// <para>
        /// 這個分組只在顯示端進行。寫入資料庫的仍然是一格一列的 <see cref="Details"/>，
        /// 所以即使這裡分組錯誤，資料庫的金額與 <see cref="TotalAmount"/> 仍然正確，
        /// 頂多是畫面顯示得不漂亮。
        /// </para>
        /// </summary>
        public IReadOnlyList<PriceGroup> GroupByPrice() => Details
            .GroupBy(d => new { d.UnitPrice, d.IsPeak })
            .OrderBy(g => g.Key.IsPeak)
            .Select(g => new PriceGroup
            {
                UnitPrice = g.Key.UnitPrice,
                IsPeak = g.Key.IsPeak,
                Hours = g.Count()
            })
            .ToList();

        /// <summary>建立一個空的計價結果。</summary>
        public static PricingResult Empty() => new();
    }

    /// <summary>
    /// 依單價分組後的一組，僅供畫面顯示。
    /// </summary>
    public class PriceGroup
    {
        /// <summary>單價。</summary>
        public int UnitPrice { get; init; }

        /// <summary>是否為尖峰。</summary>
        public bool IsPeak { get; init; }

        /// <summary>小時數。</summary>
        public int Hours { get; init; }

        /// <summary>小計。</summary>
        public int Subtotal => UnitPrice * Hours;

        /// <summary>顯示標籤，例如「尖峰 2 小時」。</summary>
        public string Label => $"{(IsPeak ? "尖峰" : "離峰")} {Hours} 小時";

        /// <summary>計算式顯示文字，例如「NT$ 500 × 2」。</summary>
        public string CalculationText => $"NT$ {UnitPrice:N0} × {Hours}";

        /// <summary>小計顯示文字。</summary>
        public string SubtotalText => $"NT$ {Subtotal:N0}";
    }
}
