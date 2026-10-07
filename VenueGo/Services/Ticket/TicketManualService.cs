using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Services.CheckIn;


namespace VenueGo.Services.Ticket
{
    public class TicketManualService : ITicketManualService
    {
        private readonly dbVenueContext _db;

        public TicketManualService(dbVenueContext db)
        {
            _db = db;
        }

        public async Task<CheckInResult> CancelAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            if(ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCancelled);
            }

            if(ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyExpired);
            }

            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCompleted);
            }

            // 防呆 --> 已入場使用過的票券不得取消
            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return CheckInResult.Fail(CheckInFailReason.InvalidSequence);
            }

            byte fromStatus = ticket.Status;
            ticket.Status = (byte)EntryTicketStatus.Cancelled;
            AddManualLog(ticketId, TicketManualLogAction.Cancel, fromStatus, ticket.Status, reasonType, reason, operatorId);
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }

        public async Task<CheckInResult> ExpireAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            if (ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyExpired);
            }

            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCancelled);
            }

            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCompleted);
            }

            // 有問題: 已使用未照預約時間出場可以手動轉逾期, 已入場無法 -> 只能等排程結束
            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return CheckInResult.Fail(CheckInFailReason.InvalidSequence);
            }

            byte fromStatus = ticket.Status;
            ticket.Status = (byte)EntryTicketStatus.Expired;
            AddManualLog(ticketId, TicketManualLogAction.Expire, fromStatus, ticket.Status, reasonType, reason, operatorId);
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }

        public async Task<CheckInResult> ReissueAsync(int ticketId, int operatorId, TicketManualLogReasonType reasonType, string? reason)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if(ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            if(ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyExpired);
            }
            if(ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCancelled);
            }
            if(ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCompleted);
            }
            if(ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return CheckInResult.Fail(CheckInFailReason.InvalidSequence);
            }
            
            byte fromStatus = ticket.Status;
            ticket.Qrtoken = $"TCK-{Guid.NewGuid():N}";
            AddManualLog(ticketId, TicketManualLogAction.Expire, fromStatus, ticket.Status, reasonType, reason, operatorId);
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }

        // 人工異動寫入 TicketStatusLog（只 Add，不 Save，讓呼叫端跟狀態變更一起存）
        private void AddManualLog(int ticketId, TicketManualLogAction action, byte fromStatus, byte toStatus,
            TicketManualLogReasonType reasonType, string? reason, int operatorId)
        {
            _db.TicketStatusLogs.Add(new TicketStatusLog
            {
                TicketId = ticketId,
                ActionType = (byte)action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ReasonType = (byte)reasonType,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                OperatorId = operatorId,
                CreatedAt = DateTime.Now
            });
        }
    }
}
