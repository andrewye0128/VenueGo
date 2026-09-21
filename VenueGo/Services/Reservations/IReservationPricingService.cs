using System.Collections.Generic;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約計價服務：唯一負責算錢的地方。
    /// <para>
    /// 【為何要集中】金額會寫進三個欄位（OrdersDetails.Subtotal、
    /// Orders.TotalAmount、Payments.Amount），而且至少四個地方要用到計價結果：
    /// 步驟 4 即時顯示金額、步驟 5 確認頁、步驟 5 寫入資料庫、編輯預約改時段後重算。
    /// 若各自乘一次單價，遲早會有一處算錯，而金額錯誤不會報錯，只會默默存進資料庫。
    /// </para>
    /// <para>
    /// 【為何是同步方法】計價不需要存取資料庫。
    /// 單價已經由 ITimeSlotService 查好並放在 TimeSlotStatus.UnitPrice 裡，
    /// 這裡只做加總。少一次資料庫往返，也避免價格在驗證與計價之間被改動。
    /// </para>
    /// </summary>
    public interface IReservationPricingService
    {
        /// <summary>
        /// 依所選時段計算明細與總金額。
        /// <para>
        /// 傳入的必須是伺服器端查證過的時段（來自 ISlotSelectionValidator 的結果），
        /// 不可直接使用前端送來的資料。
        /// </para>
        /// </summary>
        /// <param name="slots">所選時段，順序不限，方法內會自行排序。</param>
        PricingResult Calculate(IReadOnlyList<TimeSlotStatus> slots);
    }
}
