using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Services.CheckIn;


namespace VenueGo.Services.Ticket
{
    public class TicketManualService : ITicketManualService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _time;


        public TicketManualService(dbVenueContext db, ITimeService time)
        {
            _time = time;
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
            AddManualLog(ticketId, TicketManualLogAction.Reissue, fromStatus, ticket.Status, reasonType, reason, operatorId);
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }

        public async Task<TicketTransferResult> TransferAsync(int ticketId, int operatorId, string email, TicketManualLogReasonType reasonType, string? reason)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if(ticket == null)
            {
                return TicketTransferResult.Fail("找不到這張票券");
            }

            if(ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return TicketTransferResult.Fail("只有[有效]的票券可以轉讓");
            }

            var newUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim());
            if(newUser == null)
            {
                return TicketTransferResult.Fail("找不到這個 Email 的會員");
            }

            if(ticket.UserId == newUser.UserId)
            {
                return TicketTransferResult.Fail("對方已是這張票券的持有人");
            }

            //同訂單一人只能認領一張
            bool alreadyReceived = await _db.EntryTickets.AnyAsync(t => t.OrderId == ticket.OrderId && t.ReceivedUserId == newUser.UserId);
            if(alreadyReceived)
            {
                return TicketTransferResult.Fail("對方已在這筆訂單認領過票券");
            }

            byte status = ticket.Status;
            TicketHolderChange.Apply(ticket, newUser.UserId);

            //將轉讓結果 -> 新持有人寫到備註
            string logReason = $"轉讓給 {email.Trim()}";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                logReason += $"／{reason.Trim()}";
            }
            if(logReason.Length > 200)
            {
                logReason = logReason.Substring(0, 200);
            }
            AddManualLog(ticketId, TicketManualLogAction.Transfer, status, status, reasonType, logReason, operatorId);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // 剛好同時被認領（OrderId + ReceivedUserId 唯一索引）
                _db.ChangeTracker.Clear();
                return TicketTransferResult.Fail("轉讓失敗，對方可能剛好已認領同訂單的票券，請重新整理");
            }
            return TicketTransferResult.Ok();
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
                CreatedAt = _time.Now
            });
        }
    }
}
