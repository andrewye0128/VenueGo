using VenueGo.Models.Enums;

namespace VenueGo.Services.CheckIn
{
    public interface ICheckInService
    {
        /// <summary>
        /// 票券報到/離場的共用審核邏輯(牽涉到進出場掃描的實作邏輯)
        /// 前台(Vue → Api Controller)與後台(TicketController)都注入同一份實作，
        /// 差別只在呼叫時傳入的 operatorId / isManualOverride。
        /// </summary>
        Task<CheckInResult> CheckInAsync(int ticketId, int? operatorId, bool isManualOverride);
        Task<CheckInResult> CheckOutAsync(int ticketId, int? operatorId, bool isManualOverride);

        // 必有票券和員工確認
        // 設定isWaived是在收費階段只有員工免收/已收, 免收: method = null, remark必填
        Task<OvertimePaymentResult> OvertimeCheckOutAsync(int ticketId, int operatorId, bool isWaived, PaymentMethodOnSite? method, string? remark);

        Task SettleTicketAsync(int ticketId);

        // 新增:排程用,掃過所有 Valid/Used 的票,把到期的結算掉
        Task<int> SettleAllDueTicketsAsync();

        // 排程用:幫「超時已封頂但沒人處理」的訂單代建待收紀錄
        Task<int> SettleOvertimeAsync();
    }
}
