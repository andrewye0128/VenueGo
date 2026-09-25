using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using static VenueGo.Services.CheckIn.ICheckInService;

namespace VenueGo.Services.CheckIn
{
    public class CheckInService : ICheckInService
    {
        private readonly dbVenueContext _db;

        public CheckInService(dbVenueContext db)
        {
            _db = db;
        }

        public async Task SettleTicketAsync(int ticketId)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket != null) await SettleIfPastEndAsync(ticket);
        }

        public async Task<CheckInResult> CheckInAsync(int ticketId, int? operatorId, bool isManualOverride)
        {
            // 找無票券
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            await SettleIfPastEndAsync(ticket);

            // --------------------------- 驗證票券 ---------------------------
            // 第一層驗證 - 票證為不合法序號
            // 票證取消(已申請退款流程)
            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyCancelled);
            }

            // 票證逾期(已超過預約時間)
            if (ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyExpired);
            }

            // 票證已完成(預約訂單已結束)
            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyCompleted);
            }

            // 第二層驗證 - 票證合法但不符合入場條件
            // 未到預約時間
            var reservation = await GetReservationTime(ticket.OrderId);
            if (reservation != null && DateTime.Now < reservation.Value.BookingDate.ToDateTime(reservation.Value.StartTIme))
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.NotYetStartTime);
            }

            // 第三層驗證 - 最後一層判斷 - 票證出入是否異常 - 查詢最後出入場的紀錄 - 無紀錄票證轉已使用 - 成功寫入出入場紀錄
            var lastlog = await GetLastInOutLogAsync(ticketId);
            //無紀錄或以出場達成入場條件
            bool isVaild = lastlog == null || lastlog.Action == (byte)CheckInAction.CheckOut;
            if (isVaild && ticket.Status == (byte)EntryTicketStatus.Valid)
            {
                ticket.Status = (byte)EntryTicketStatus.Used;
            }

            // 新增出入廠動作和紀錄
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.CheckIn,
                ActionTime = DateTime.Now,
                IsValid = isVaild,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId
            });

            await _db.SaveChangesAsync();
            // 回傳驗證結果(前台顯示進出成功或是失敗)
            return isVaild ? CheckInResult.Ok() : CheckInResult.Fail(CheckInFailReason.InvalidSequence);
        }

        public async Task<CheckInResult> CheckOutAsync(int ticketId, int? operatorId, bool isManualOverride)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            await SettleIfPastEndAsync(ticket);

            // 出入狀態判斷
            var lastLog = await GetLastInOutLogAsync(ticketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;

            // 終態判斷(是否已有終態)
            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyCancelled);
            if (ticket.Status == (byte)EntryTicketStatus.Completed)
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyCompleted);
            // 已失效且人不在場內 → 已失效(已完成)
            if (ticket.Status == (byte)EntryTicketStatus.Expired && !isInside)
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyExpired);

            // 已失效但人還在場內（超時失效）→ 繳費放行，唯一允許的動作
            /////////////////////
            // TODO：之後接超時費用

            /////////////////////

            bool isValid = isInside;

            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.CheckOut,
                ActionTime = DateTime.Now,
                IsValid = isValid,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId
            });
            await _db.SaveChangesAsync();

            return isValid ? CheckInResult.Ok() : CheckInResult.Fail(CheckInFailReason.InvalidSequence);
        }

        // 專給人工取消(後台) --> 自動取取消功能已同步在取消票券
        public async Task<CheckInResult> CancelAsync(int ticketId, int operatorId, bool isManualOverride)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            // 重複取消也會有錯誤訊息(出現已取消)
            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCancelled);
            }

            // 終態無法執行取消動作
            if (ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyExpired);
            }

            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return CheckInResult.Fail(CheckInFailReason.AlreadyCompleted);
            }

            // 防呆 --> 以入場使用過的票券不得取消
            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return CheckInResult.Fail(CheckInFailReason.InvalidSequence);
            }

            //紀錄人工取消 --> CheckInLog以改成對於這張票券的動作狀態
            ticket.Status = (byte)EntryTicketStatus.Cancelled;
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.ManualCancel,
                ActionTime = DateTime.Now,
                IsValid = true,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId
            });
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }

        public async Task<CheckInResult> ExpireAsync(int ticketId, int operatorId, bool isManualOverride)
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

            // 有問題: 已使用未照預約時間出場 / 未在預約入場時段入場 --> 統一逾時
            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return CheckInResult.Fail(CheckInFailReason.InvalidSequence);
            }

            ticket.Status = (byte)EntryTicketStatus.Expired;
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.ManualExpire,
                ActionTime = DateTime.Now,
                IsValid = true,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId
            });
            await _db.SaveChangesAsync();
            return CheckInResult.Ok();
        }



        // 共用查詢, 能檢查預約時間減查早到或是超時/未到逾時
        private async Task<(DateOnly BookingDate, TimeOnly StartTIme, TimeOnly EndTime)?> GetReservationTime(int orderId)
        {
            // 根據票券查詢訂單對應的預約時間
            var data = await (from o in _db.Orders
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              where o.OrderId == orderId
                              select new { r.BookingDate, r.StartTime, r.EndTime })
                             .FirstOrDefaultAsync();

            return data == null ? null : (data.BookingDate, data.StartTime, data.EndTime);
        }


        private async Task<CheckInLog?> GetLastInOutLogAsync(int ticketId)
        {
            // 查詢出入場紀錄 --> 需要isValid判斷(ex: 早到也會有一筆失敗進場紀錄, 再刷一次會出現重複進場)
            return await _db.CheckInLogs
                .Where(l => l.TicketId == ticketId && l.IsValid &&
                    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .OrderByDescending(l => l.ActionTime)
                .FirstOrDefaultAsync();
        }

        // 所有超過預約區間的情況(票券會變失效) -> 終態結算
        // 透過現在時間 > EndTime && 現在的票券狀態 && 出入場狀態
        private async Task<bool> SettleIfPastEndAsync(EntryTicket ticket)
        {
            // 只有 有效 / 已使用 需要結算，失效、取消、完成都已經是終態
            if (ticket.Status != (byte)EntryTicketStatus.Valid &&
                ticket.Status != (byte)EntryTicketStatus.Used)
                return false;

            var reservation = await GetReservationTime(ticket.OrderId);
            if (reservation == null) return false;

            var endAt = reservation.Value.BookingDate.ToDateTime(reservation.Value.EndTime);
            if (DateTime.Now <= endAt) return false;

            var lastLog = await GetLastInOutLogAsync(ticket.TicketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;

            // 判斷是否使用 -> 未使用失效(預約未到)
            if (ticket.Status == (byte)EntryTicketStatus.Valid)
                ticket.Status = (byte)EntryTicketStatus.Expired;
            // 已使用且以人在場內 -> 超時失效
            else if (isInside)
                ticket.Status = (byte)EntryTicketStatus.Expired;
            // 使用完畢
            else
                ticket.Status = (byte)EntryTicketStatus.Completed;

            await _db.SaveChangesAsync();
            return true;
        }


        private async Task<CheckInResult> LogAndFailAsync(int ticketId, CheckInAction action, int? operatorId, bool isManualOverride, CheckInFailReason reason)
        {
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)action,
                ActionTime = DateTime.Now,
                IsValid = false,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId
            });
            await _db.SaveChangesAsync();
            return CheckInResult.Fail(reason);
        }
    }
}