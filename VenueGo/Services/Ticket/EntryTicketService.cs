using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Entities;
using VenueGo.Services.Time;

namespace VenueGo.Services.Ticket
{
    public class EntryTicketService : IEntryTicketService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _time;
        private const int TransferCooldownSeconds = 60;
        private const int TransferMaxPerTarget = 3;
        private const int TransferMaxPerSenderHour = 10;
        private readonly CEntryTicketFactory _entryTicket;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        ILogger<EntryTicketService> _logger;

        public EntryTicketService(dbVenueContext db, ITimeService time, CEntryTicketFactory entryTicket, IEmailService email, IConfiguration config, ILogger<EntryTicketService> logger)
        {
            _db = db; //建構子注入: 跟訂單Controller一樣和dbVenueContext建立關聯
            _time = time;
            _entryTicket = entryTicket;
            _email = email;
            _config = config;
            _logger = logger;
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


        public Task<TicketTransferResult> TransferCreateAsync(int ticketId, int userId, string email)
        {
            return TransferCoreAsync(ticketId, userId, email, resend: false);
        }

        // 票券轉移和重寄,透過resend判斷
        private async Task<TicketTransferResult> TransferCoreAsync(int ticketId, int userId, string email, bool resend)
        {
            DateTime now = _time.Now;
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return TicketTransferResult.Fail("找不到這張票券");
            }

            if (ticket.UserId != userId)
            {
                return TicketTransferResult.Fail("這不是你的票券");
            }

            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return TicketTransferResult.Fail("只有[有效]的票券可以轉讓");
            }

            var booking = await (from o in _db.Orders
                                 join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                 join v in _db.Venues on r.VenueId equals v.VenueId
                                 where o.OrderId == ticket.OrderId
                                 select new { r.BookingDate, r.StartTime, r.EndTime, v.VenueName }).FirstOrDefaultAsync();

            if (booking == null)
            {
                return TicketTransferResult.Fail("找不到預約資料");
            }

            DateTime reservationEnd = booking.BookingDate.ToDateTime(booking.EndTime);
            if (reservationEnd <= now)
            {
                return TicketTransferResult.Fail("預約已結束，無法轉讓");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                return TicketTransferResult.Fail("請輸入對方的Email");
            }
            string emailCheck = email.Trim().ToLower();
            var tomember = await _db.Users.Where(u => u.Email == emailCheck && u.Status == UserStatuses.Active).AsNoTracking().FirstOrDefaultAsync();
            if (tomember == null)
            {
                return TicketTransferResult.Ok();
            }

            if (tomember.UserId == userId)
            {
                return TicketTransferResult.Fail("不能轉讓給自己");
            }

            // 檢查使否有還在期限內的(待)接受票券轉移
            var pending = await _db.TicketTransfers.FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Status == (byte)TicketTransferStatus.Pending);

            if (!resend && pending != null && pending.ExpiresAt > now && pending.FromUserId == userId && pending.ToUserId == tomember.UserId)
            {
                return TicketTransferResult.Ok();
            }

            // 冷卻
            DateTime? lastCreateAt = await _db.TicketTransfers
                .Where(t => t.TicketId == ticketId && t.ToUserId == tomember.UserId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => (DateTime?)t.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastCreateAt != null && (now - lastCreateAt.Value).TotalSeconds < TransferCooldownSeconds)
            {
                return TicketTransferResult.Fail("操作太頻繁，請稍後再試");
            }

            // 次數上限：同票同對象 24 小時內最多 3 筆；同轉出者 1 小時內最多 10 筆
            DateTime since24h = now.AddHours(-24);
            int sameTargetCount = await _db.TicketTransfers.CountAsync(t =>
                t.TicketId == ticketId && t.ToUserId == tomember.UserId && t.CreatedAt > since24h);
            if (sameTargetCount >= TransferMaxPerTarget)
            {
                return TicketTransferResult.Fail("這張票 24 小時內最多寄送 3 次，請稍後再試");
            }

            DateTime since1h = now.AddHours(-1);
            int senderCount = await _db.TicketTransfers.CountAsync(t =>
                t.FromUserId == userId && t.CreatedAt > since1h);
            if (senderCount >= TransferMaxPerSenderHour)
            {
                return TicketTransferResult.Fail("你寄送通知的次數過多，請稍後再試");
            }

            if (pending != null)
            {
                pending.Status = pending.ExpiresAt > now
                    ? (byte)TicketTransferStatus.Cancelled
                    : (byte)TicketTransferStatus.Expired;
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
                ToUserId = tomember.UserId,
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
                return TicketTransferResult.Fail("產生驗證碼失敗，請稍後再試");
            }

            bool smtpReady = !string.IsNullOrWhiteSpace(_config["SmtpSettings:Host"]);
            string fromNmae = await _db.Users.Where(u => u.UserId == userId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync() ?? "會員";
            string baseUrl = (_config["Frontend:BaseUrl"] ?? "").TrimEnd('/');
            string link = baseUrl + "/tickets";

            string subject = "有人想把票券轉讓給你";
            string body = $@"
                            <p>{WebUtility.HtmlEncode(fromNmae)}想把一張票券轉讓給你</p>
                            <ul>
                                <li>場館：{WebUtility.HtmlEncode(booking.VenueName)}</li>
                                <li>日期：{booking.BookingDate:yyyy-MM-dd}</li>
                                <li>時段：{booking.StartTime:HH\:mm} ~ {booking.EndTime:HH\:mm}</li>
                            </ul>
                            <p>驗證碼：<b style=""font-size:20px;letter-spacing:2px"">{transfer.TransferCode}</b></p>
                            <p>請在 {transfer.ExpiresAt:yyyy-MM-dd HH:mm}（台北時間）前，登入會員後輸入驗證碼領取：</p>
                            <p><a href=""{WebUtility.HtmlEncode(link)}"">前往領取</a></p>
                            <p>如果你不認識對方或沒有預期收到這封信，請直接忽略。</p>";
            if (!smtpReady)
            {
                _logger.LogWarning("未設定 SMTP，略過寄信。收件人 {To}，驗證碼 {Code}，內容：{Body}",
                    tomember.Email, transfer.TransferCode, body);
                return TicketTransferResult.Ok();
            }

            try
            {
                await _email.SendEmailAsync(tomember.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "轉讓通知信寄送失敗，TransferId={TransferId}", transfer.TransferId);

                // 寄不出去就作廢，不留下 A 看不到碼、B 沒收到信的死單
                transfer.Status = (byte)TicketTransferStatus.Cancelled;
                await _db.SaveChangesAsync();
                return TicketTransferResult.Fail("通知信寄送失敗，請稍後再試");
            }

            return TicketTransferResult.Ok();
        }

        public async Task<TicketTransferResult> TransferResendAsync(int ticketId, int userId)
        {
            var pending = await _db.TicketTransfers.AsNoTracking().FirstOrDefaultAsync(t =>
                t.TicketId == ticketId
                && t.Status == (byte)TicketTransferStatus.Pending
                && t.FromUserId == userId);
            if (pending == null || pending.ToUserId == null)
            {
                return TicketTransferResult.Fail("沒有可重新寄送的轉讓");
            }

            string? toEmail = await _db.Users
                .Where(u => u.UserId == pending.ToUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();
            if (toEmail == null)
            {
                return TicketTransferResult.Fail("無法重新寄送，請重新轉讓");
            }

            return await TransferCoreAsync(ticketId, userId, toEmail, resend: true);
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

            // 不洩漏這組密碼存在和到不到童訊息
            if (pending.ToUserId != userId)
            {
                return TicketTransferResult.Fail("驗證碼錯誤");
            }

            if (pending.ExpiresAt <= now)
            {
                pending.Status = (byte)TicketTransferStatus.Expired;
                await _db.SaveChangesAsync();
                return TicketTransferResult.Fail("驗證碼已過期");
            }

            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(e => e.TicketId == pending.TicketId);
            if (ticket == null)
            {
                return TicketTransferResult.Fail("無此票券");
            }

            if (ticket.Status != (byte)EntryTicketStatus.Valid)
            {
                return TicketTransferResult.Fail("只有[有效]的票券可以領取");
            }

            if (ticket.UserId != pending.FromUserId)
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
                int affected = await _db.TicketTransfers
                            .Where(t => t.TransferId == pending.TransferId 
                                    && t.ToUserId == userId
                                    && t.Status == (byte)TicketTransferStatus.Pending)
                            .ExecuteUpdateAsync(s => s
                            .SetProperty(t => t.Status, (byte)TicketTransferStatus.Completed)
                            .SetProperty(t => t.CompletedAt, now)
                            );
                if (affected == 0)
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