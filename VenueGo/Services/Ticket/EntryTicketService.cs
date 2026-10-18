using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using VenueGo.Data;
using VenueGo.Models.CheckinModels;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Services.Time;

namespace VenueGo.Services.Ticket
{
    public class EntryTicketService : IEntryTicketService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _time;
        private readonly CEntryTicketFactory _entryTicket;

        public EntryTicketService(dbVenueContext db, ITimeService time, CEntryTicketFactory entryTicket)
        {
            _db = db; //建構子注入: 跟訂單Controller一樣和dbVenueContext建立關聯
            _time = time;
            _entryTicket = entryTicket;
        }


        public async Task<(string Message, int TicketCount)> CreateForOrderAsync(int orderId)
        {
            if (await _db.EntryTickets.AnyAsync(t => t.OrderId == orderId))
                return (Message: "票卷已在處理中, 請勿重複申請", TicketCount: 0);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
                return (Message: "找不到訂單", TicketCount: 0);

            int count = order.PersonMount > 0 ? order.PersonMount : 1;

            for (int i = 0; i < count; i++)
            {
                _db.EntryTickets.Add(new EntryTicket
                {
                    OrderId = orderId,
                    Qrtoken = $"TCK-{Guid.NewGuid():N}",
                    Status = (byte)EntryTicketStatus.Valid,
                    CreatedAt = _time.Now,
                    UserId = order.UserId
                });
            }
            await _db.SaveChangesAsync();
            return (Message: "成功產生票券", TicketCount: count);
        }

        public async Task<int> CancelByOrderAsync(int orderId)
        {
            var tickets = await _db.EntryTickets
                .Where(t => t.OrderId == orderId && t.Status == (byte)EntryTicketStatus.Valid)
                .ToListAsync();

            if (tickets.Count > 0)
                foreach (var t in tickets)
                    t.Status = (byte)EntryTicketStatus.Cancelled;
            else
                return 0;
            await _db.SaveChangesAsync();

            return tickets.Count;
        }



        // 票券轉移
        public async Task<TransferCodeResult> TransferCreateAsync(int ticketId, int userId)
        {
            DateTime now = _time.Now;
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return TransferCodeResult.Fail("找不到這張票券");
            }

            if (ticket.UserId != userId)
            {
                return TransferCodeResult.Fail("這不是你的票券");
            }

            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return TransferCodeResult.Fail("只有[有效]的票券可以轉讓");
            }

            var booking = await (from o in _db.Orders
                                 join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                 where o.OrderId == ticket.OrderId
                                 select new { r.BookingDate, r.EndTime }).FirstOrDefaultAsync();

            if (booking == null)
            {
                return TransferCodeResult.Fail("找不到預約資料");
            }

            DateTime reservationEnd = booking.BookingDate.ToDateTime(booking.EndTime);
            if (reservationEnd <= now)
            {
                return TransferCodeResult.Fail("預約已結束，無法轉讓");
            }

            // 檢查使否有還在期限內的(待)接受票券轉移
            var pending = await _db.TicketTransfers.FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Status == (byte)TicketTransferStatus.Pending);

            if (pending != null)
            {
                if (pending.ExpiresAt > now && pending.FromUserId == userId)
                {
                    return TransferCodeResult.Ok(pending.TransferCode, pending.ExpiresAt);
                }
                pending.Status = (byte)TicketTransferStatus.Expired;
                await _db.SaveChangesAsync();
            }

            // 期限1天,不超過預約時間
            DateTime expireTime = now.AddHours(24);
            if (reservationEnd < expireTime)
            {
                expireTime = reservationEnd;
            }


            var transfer = new TicketTransfer
            {
                TicketId = ticketId,
                FromUserId = userId,
                ToUserId = null,
                TransferCode = GenereateTransferCode(),
                Status = (byte)TicketTransferStatus.Pending,
                CreatedAt = now,
                ExpiresAt = expireTime,
            };
            _db.TicketTransfers.Add(transfer);

            try
            {
                await _db.SaveChangesAsync();
            }

            catch (DbUpdateException)
            {
                // 撞到唯一索引（驗證碼重複，或同時按了兩次）
                _db.Entry(transfer).State = EntityState.Detached;
                return TransferCodeResult.Fail("產生驗證碼失敗，請稍後再試");
            }

            return TransferCodeResult.Ok(transfer.TransferCode, transfer.ExpiresAt);
        }

        public async Task<TicketTransferResult> TransferCancelAsync(int ticketId, int userId)
        {
            DateTime now = _time.Now;

            var pending = await _db.TicketTransfers
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Status == (byte)TicketTransferStatus.Pending);
            if (pending == null)
            {
                return TicketTransferResult.Fail("沒有待領取的轉讓");
            }

            if (pending.FromUserId != userId)
            {
                return TicketTransferResult.Fail("這不是你發起的轉讓");
            }

            if (pending.ExpiresAt <= now)
            {
                pending.Status = (byte)TicketTransferStatus.Expired;
                await _db.SaveChangesAsync();
                return TicketTransferResult.Fail("這筆轉讓已過期");
            }

            // 為了併發風險直接執行指令: 直接將票券為待處理的票券更新為取消(如果在原本寫法: 查詢票券 => 更改狀態(在改狀態的期間票券被接收會票券在接收人身上但被取消))
            int affected = await _db.TicketTransfers
                .Where(t => t.TransferId == pending.TransferId && t.Status == (byte)TicketTransferStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, (byte)TicketTransferStatus.Cancelled));

            // 如果是0表是已被搶先接收
            if (affected == 0)
            {
                return TicketTransferResult.Fail("轉讓已被處理，請重新整理");
            }
            return TicketTransferResult.Ok();
        }

        public async Task<TicketTransferResult> TransferRedeemAsync(string code, int userId)
        {
            DateTime now = _time.Now;

            if (string.IsNullOrWhiteSpace(code))
            {
                return TicketTransferResult.Fail("請輸入驗證碼");
            }

            code = code.Trim().ToUpper();

            var pending = await _db.TicketTransfers.FirstOrDefaultAsync(t => t.TransferCode == code && t.Status == (byte)TicketTransferStatus.Pending);
            if (pending == null)
            {
                return TicketTransferResult.Fail("驗證碼錯誤");
            }

            if(pending.ExpiresAt <= now)
            {
                pending.Status = (byte)TicketTransferStatus.Expired;
                await _db.SaveChangesAsync();
                return TicketTransferResult.Fail("驗證碼已過期");
            }

            if (pending.FromUserId == userId)
            {
                return TicketTransferResult.Fail("領取者和轉發者不得為同個人");
            }

            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(e => e.TicketId == pending.TicketId);
            if (ticket == null)
            {
                return TicketTransferResult.Fail("無此票券");
            }

            if(ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return TicketTransferResult.Fail("只有[有效]的票券可以領取");
            }

            if(ticket.UserId != pending.FromUserId)
            {
                return TicketTransferResult.Fail("這張票券已不在轉讓人手上");
            }

            bool alreadyReceived = await _db.EntryTickets.AnyAsync(t => t.OrderId == ticket.OrderId && t.ReceivedUserId == userId);
            if (alreadyReceived)
            {
                return TicketTransferResult.Fail("你已在這筆訂單認領過票券");
            }


            // 轉讓紀錄與票券持有人要在同一個交易
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // 和取消相同防止併發
                int affected = await _db.TicketTransfers.Where(t => t.TransferId == pending.TransferId && t.Status == (byte)TicketTransferStatus.Pending)
                            .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Status, (byte)TicketTransferStatus.Completed)
                            .SetProperty(t => t.ToUserId, userId)
                            .SetProperty(t => t.CompletedAt, now)
                            );
                if(affected == 0)
                {
                    await tx.RollbackAsync();
                    return TicketTransferResult.Fail("驗證碼已被使用或已取消");
                }
                _entryTicket.ChangeHolder(ticket, userId);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            // 例如同時間同訂單已認領（OrderId + ReceivedUserId 唯一索引）
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                return TicketTransferResult.Fail("領取失敗，請稍後再試");
            }
            return TicketTransferResult.Ok();
        }

        private static string GenereateTransferCode()
        {
            const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
            char[] code = new char[8];
            for (int i = 0; i < code.Length; i++)
            {
                code[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            }

            return new string(code);
        }
    }

}