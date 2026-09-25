using VenueGo.Models.Enums;

namespace VenueGo.Services.CheckIn
{
    public interface ICheckInService
    {

        /// <summary>
        /// 報到審核結果。Success 為 true 時 Reason 一定是 null；為 false 時 Reason 一定有值。
        /// </summary>
        public record CheckInResult(bool Success, CheckInFailReason? Reason)
        {
            public static CheckInResult Ok() => new(true, null);
            public static CheckInResult Fail(CheckInFailReason reason) => new(false, reason);
        }

        /// <summary>
        /// 票券報到/離場/取消/逾期的共用審核邏輯。
        /// 前台(Vue → Api Controller)與後台(TicketController)都注入同一份實作，
        /// 差別只在呼叫時傳入的 operatorId / isManualOverride。
        /// </summary>
        Task<CheckInResult> CheckInAsync(int ticketId, int? operatorId, bool isManualOverride);
        Task<CheckInResult> CheckOutAsync(int ticketId, int? operatorId, bool isManualOverride);
        Task<CheckInResult> CancelAsync(int ticketId, int operatorId, bool isManualOverride);
        Task<CheckInResult> ExpireAsync(int ticketId, int operatorId, bool isManualOverride);

        Task SettleTicketAsync(int ticketId);
    }
}
