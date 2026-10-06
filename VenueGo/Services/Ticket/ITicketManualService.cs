using VenueGo.Models.Enums;
using VenueGo.Services.CheckIn;

namespace VenueGo.Services.Ticket
{
    /// <summary>
    /// 後台對票券的人工異動：直接決定這張票的狀態，並寫入 TicketStatusLog。
    /// 跟進出場(掃碼)不同：掃碼寫 CheckInLog，這裡寫 TicketStatusLog, 所以抽離。
    /// 作廢 = 取消 + 原因類別(重複或誤購 / 疑似異常)，不另設狀態。
    /// 備註「選其他必填」由呼叫端(Controller)驗證。
    /// </summary>
    public interface ITicketManualService
    {
        Task<CheckInResult> CancelAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason);
        Task<CheckInResult> ExpireAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason);
        Task<CheckInResult> ReissueAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason);
    }
}
