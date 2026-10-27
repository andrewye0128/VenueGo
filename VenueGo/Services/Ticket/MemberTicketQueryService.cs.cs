using VenueGo.Data;
using VenueGo.Dtos.TicketDtos;
using Microsoft.EntityFrameworkCore;
using VenueGo.Enums;
using VenueGo.Services.Time;

namespace VenueGo.Services.Ticket
{
    public class MemberTicketQueryService : IMemberTicketQueryService
    {

        private readonly dbVenueContext _db;
        private readonly ITimeService _time;
        private readonly QrTokenService _qrTokenService;

        public MemberTicketQueryService(dbVenueContext db, ITimeService time, QrTokenService qrTokenService)
        {
            _db = db;
            _time = time;
            _qrTokenService = qrTokenService;
        }


        public async Task<List<MemberTicketDto>> GetMyTicketsAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            // 持有人 = EntryTicket.UserId；舊資料是 null 就退回訂購人 (Order.UserId)
            // 撈：我持有的票 + 我訂購但已轉給別人的票；已取消的票在這裡就過濾掉
            // 移除傳入QRToken改由30秒組成QR(v1.{ticketId}.{時間窗}.{簽章})
            var rows = await (from t in _db.EntryTickets
                              join o in _db.Orders on t.OrderId equals o.OrderId
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              join v in _db.Venues on r.VenueId equals v.VenueId
                              join s in _db.SportTypes on v.SportTypeId equals s.SportTypeId
                              where t.Status != (byte)EntryTicketStatus.Cancelled
                                 && ((t.UserId ?? o.UserId) == userId || o.UserId == userId)
                              orderby r.BookingDate descending, r.StartTime descending
                              select new
                              {
                                  t.TicketId,
                                  t.Status,
                                  HolderId = t.UserId ?? o.UserId,
                                  r.BookingDate,
                                  r.StartTime,
                                  r.EndTime,
                                  v.VenueName,
                                  s.SportName
                              })
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);
            var now = _time.Now;
            var ticketIds = rows.Select(x => x.TicketId).ToList();
            var insideIds = await GetInsideTicketIdsAsync(ticketIds, cancellationToken);
            var pendingMap = await _db.TicketTransfers
                .AsNoTracking()
                .Where(p => ticketIds.Contains(p.TicketId)
                    && p.FromUserId == userId
                    && p.Status == (byte)TicketTransferStatus.Pending
                    && p.ExpiresAt > now)
                .ToDictionaryAsync(p => p.TicketId, cancellationToken);

            var toIds = pendingMap.Values
                .Where(p => p.ToUserId != null)
                .Select(p => p.ToUserId!.Value)
                .Distinct().ToList();
            var toEmails = await _db.Users.AsNoTracking()
                .Where(u => toIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

            return rows.Select(x =>
            {
                string status = ToMemberStatus(
                    x.Status, x.HolderId == userId, insideIds.Contains(x.TicketId),
                    x.BookingDate.ToDateTime(x.EndTime), now);

                pendingMap.TryGetValue(x.TicketId, out var pending);
                string? maskedTo = null;
                if (status == "available" && pending?.ToUserId != null
                    && toEmails.TryGetValue(pending.ToUserId.Value, out var toEmail))
                {
                    maskedTo = MaskEmail(toEmail);
                }
                return new MemberTicketDto
                {
                    Id = x.TicketId,
                    VenueName = x.VenueName,
                    SportType = x.SportName,
                    Date = x.BookingDate.ToString("yyyy/MM/dd"),
                    TimeRange = $"{x.StartTime:HH\\:mm}-{x.EndTime:HH\\:mm}",
                    Status = status,
                    TransferToMasked = maskedTo,
                    TransferExpiresAt = status == "available" ? pending?.ExpiresAt : null,
                };
            }).ToList();            
        }

        private static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            if (at <= 0) return "***";
            return email[0] + "***" + email[at..];
        }


        public async Task<List<IncomingTransferDto>> GetIncomingTransfersAsync(
            int userId, CancellationToken cancellationToken = default)
        {
            var now = _time.Now;

            var rows = await (from tr in _db.TicketTransfers
                              join t in _db.EntryTickets on tr.TicketId equals t.TicketId
                              join o in _db.Orders on t.OrderId equals o.OrderId
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              join v in _db.Venues on r.VenueId equals v.VenueId
                              join s in _db.SportTypes on v.SportTypeId equals s.SportTypeId
                              join u in _db.Users on tr.FromUserId equals u.UserId
                              where tr.ToUserId == userId
                                 && tr.Status == (byte)TicketTransferStatus.Pending
                                 && tr.ExpiresAt > now
                                 && t.Status == (byte)EntryTicketStatus.Valid
                                 && t.UserId == tr.FromUserId
                              orderby tr.ExpiresAt
                              select new
                              {
                                  FromName = u.Name,
                                  v.VenueName,
                                  s.SportName,
                                  r.BookingDate,
                                  r.StartTime,
                                  r.EndTime,
                                  tr.ExpiresAt
                              })
                             .AsNoTracking()
                             .ToListAsync(cancellationToken);

            return rows.Select(x => new IncomingTransferDto
            {
                FromName = x.FromName,
                VenueName = x.VenueName,
                SportType = x.SportName,
                Date = x.BookingDate.ToString("yyyy/MM/dd"),
                TimeRange = $"{x.StartTime:HH\\:mm}-{x.EndTime:HH\\:mm}",
                ExpiresAt = x.ExpiresAt
            }).ToList();
        }


        // 按下使用QRcode
        public async Task<TicketQrDto?> GetQrAsync(
           int ticketId, int userId, CancellationToken cancellationToken = default)
        {
            // 只有「現在的持有人」查得到；不是自己的票、不存在、已取消，都是 null
            var row = await (from t in _db.EntryTickets
                             join o in _db.Orders on t.OrderId equals o.OrderId
                             join r in _db.Reservations on o.ReservationId equals r.ReservationId
                             where t.TicketId == ticketId
                                && t.Status != (byte)EntryTicketStatus.Cancelled
                                && (t.UserId ?? o.UserId) == userId
                             select new { t.TicketId, t.Qrtoken, t.Status, r.BookingDate, r.EndTime })
                            .AsNoTracking()
                            .FirstOrDefaultAsync(cancellationToken);
            if (row == null) return null;

            var insideIds = await GetInsideTicketIdsAsync(new List<int> { ticketId }, cancellationToken);
            string status = ToMemberStatus(
                row.Status, true, insideIds.Contains(ticketId),
                row.BookingDate.ToDateTime(row.EndTime), _time.Now);

            // 只有可使用或場內的票可以出示 QR
            if (status != "available" && status != "inside") return null;

            var (payload, expiresAt) = _qrTokenService.Generate(row.TicketId, row.Qrtoken);
            return new TicketQrDto
            {
                Payload = payload,
                ExpiresAt = expiresAt,
                Status = status
            };
        }

        private async Task<HashSet<int>> GetInsideTicketIdsAsync(
           List<int> ticketIds, CancellationToken cancellationToken)
        {
            var lastActions = await _db.CheckInLogs
                .AsNoTracking()
                .Where(l => ticketIds.Contains(l.TicketId) && l.IsValid
                    && (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .GroupBy(l => l.TicketId)
                .Select(g => new
                {
                    TicketId = g.Key,
                    Action = g.OrderByDescending(l => l.ActionTime).Select(l => l.Action).FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return lastActions
                .Where(x => x.Action == (byte)CheckInAction.CheckIn)
                .Select(x => x.TicketId)
                .ToHashSet();
        }

        private static string ToMemberStatus(byte dbStatus, bool heldByMe, bool isInside, DateTime endAt, DateTime now)
        {
            // 票已經不在我手上（我訂購但轉給別人了）
            if (!heldByMe) return "transferred";

            // 人還在場內：不管有沒有超過預約結束時間（超時時 DB 可能已被標成 Expired），都還要能出示 QR 離場
            if (isInside && (dbStatus == (byte)EntryTicketStatus.Used
                          || dbStatus == (byte)EntryTicketStatus.Expired))
                return "inside";

            // 已離場完成
            if (dbStatus == (byte)EntryTicketStatus.Completed) return "used";

            // 已標記失效且人不在場內
            if (dbStatus == (byte)EntryTicketStatus.Expired) return "expired";

            // 入場過、現在在場外：時段內可以再次入場，時段過了就算已使用
            if (dbStatus == (byte)EntryTicketStatus.Used)
                return endAt < now ? "used" : "available";

            // 還沒入場過（Valid）：時段沒過是可使用；還是 Valid 但時段已過（排程每 30 分鐘才結算，中間有空窗）算失效
            return dbStatus == (byte)EntryTicketStatus.Valid && endAt >= now ? "available" : "expired";
        }

    }
}
