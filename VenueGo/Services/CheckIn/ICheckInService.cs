using VenueGo.Models.Enums;

namespace VenueGo.Services.CheckIn
{
    public interface ICheckInService
    {
        /// <summary>
        /// 票券報到/離場/取消/逾期的共用審核邏輯。
        /// 前台(Vue → Api Controller)與後台(TicketController)都注入同一份實作，
        /// 差別只在呼叫時傳入的 operatorId / isManualOverride。
        /// </summary>
        Task<CheckInResult> CheckInAsync(int ticketId, int? operatorId, bool isManualOverride);
        Task<CheckInResult> CheckOutAsync(int ticketId, int? operatorId, bool isManualOverride);


        // 人工異動：改寫 TicketStatusLog，一定要帶原因類別；備註選「其他」時由呼叫端確保必填
        //Task<CheckInResult> CancelAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason);
        //Task<CheckInResult> ExpireAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason);

        Task SettleTicketAsync(int ticketId);

        // 新增:排程用,掃過所有 Valid/Used 的票,把到期的結算掉
        Task<int> SettleAllDueTicketsAsync();
    }
}
