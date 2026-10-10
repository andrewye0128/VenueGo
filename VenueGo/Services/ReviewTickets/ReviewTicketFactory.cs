using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Models.ReviewModels;

namespace VenueGo.Services.ReviewTickets
{
    // 一個實作、兩個門：報到系統看到 IVisitReviewTicketFactory，
    // 訂單／付款系統看到 IBookingReviewTicketFactory，各自只看得到自己該叫的方法。
    //
    // 文件註解寫在「介面」上，不寫在這裡：呼叫端拿到的是介面，
    // IntelliSense 顯示的也是介面上的註解。<inheritdoc/> 讓這邊直接沿用，
    // 不會出現兩份說明各說各話的情況。
    public sealed class ReviewTicketFactory(dbVenueContext db, ITimeService timeService)
        : IVisitReviewTicketFactory, IBookingReviewTicketFactory
    {
        private readonly dbVenueContext _db = db;
        private readonly ITimeService _timeService = timeService;

        // ── 現場評論憑證（10/9 改版：限會員）──────────────────────────
        //
        //  規則（昱 10/8～10/9 定案）：
        //    1. 憑證屬於「領票的會員」＝ EntryTicket.ReceivedUserId。沒有領票人（null）就不建。
        //    2. 同一張訂單、同一位會員只有一張憑證：就算一個人拿著好幾張票、每張都報到過，也只有一次評論機會。
        //       資料庫有 UQ_ReviewPerVisit_UserId_OrderId 擋著，這裡先查一次，正常情況不會撞到。
        //    3. 要有「預約時段內」的有效入場紀錄。
        //    4. 憑證建立之後永遠屬於當時的會員，之後票券怎麼轉都不改。
        //  （以前用 QRToken 判斷「這張票建過沒」，一張票一張憑證；現在改用「訂單＋會員」。）

        /// <inheritdoc/>
        public async Task<bool> CreateReviewPerVisitAsync(string? token)
        {
            if (token == null) return false;

            var entry = await _db.EntryTickets.FirstOrDefaultAsync(t => t.Qrtoken == token);
            if (entry == null) return false;
            if ((EntryTicketStatus)entry.Status is not 
                (EntryTicketStatus.Used or EntryTicketStatus.Completed)) return false; // 要先把票券狀態改為 Used 再呼叫

            // 規則 1：沒有領票的會員就沒有憑證
            if (entry.ReceivedUserId is not int userId) return false;

            // 規則 2：這張票建過，或這位會員在這張訂單已經有憑證了
            bool exists = await _db.ReviewPerVisits.AnyAsync(v =>
                v.TicketId == entry.TicketId || (v.UserId == userId && v.OrderId == entry.OrderId));
            if (exists) return false;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == entry.OrderId);
            if (order == null) return false;

            var reservation = await _db.Reservations.FirstOrDefaultAsync(s => s.ReservationId == order.ReservationId);
            if (reservation == null) return false;

            var venue = await _db.Venues.FirstOrDefaultAsync(n => n.VenueId == reservation.VenueId);
            if (venue == null) return false;

            DateTime rentStart = reservation.BookingDate.ToDateTime(reservation.StartTime);
            DateTime rentEnd = reservation.BookingDate.ToDateTime(reservation.EndTime);

            // 規則 4（10/10 新增）：網站時間還沒到預約時段，不建憑證。
            // 正常流程（報到當下呼叫）不會發生。會發生的是「入場紀錄的時間比現在還晚」這種資料，例如測試時用 SQL 補的紀錄。
            // 照樣建立的話 CreatedAt 會早於 RentStartTime，違反資料表的 CHK_ReviewPerVisit_CreatedAt_Logic，
            // 存檔時丟例外，而且會一路丟回呼叫端（組員的報到流程），所以要在存檔前先擋下來。
            DateTime now = _timeService.Now;
            if (now < rentStart) return false;

            // 原本（10/10 加上「入場紀錄不能晚於現在」）：
            // // 規則 3：預約時段內有一筆成功的入場紀錄
            // bool checkedInDuringRent = await _db.CheckInLogs.AnyAsync(c =>
            //     c.TicketId == entry.TicketId
            //     && c.Action == (byte)CheckInAction.CheckIn
            //     && c.IsValid
            //     && c.ActionTime >= rentStart
            //     && c.ActionTime < rentEnd);
            // if (!checkedInDuringRent) return false;

            // 規則 3：預約時段內有一筆成功的入場紀錄，而且這筆紀錄已經發生了（不晚於現在）
            bool checkedInDuringRent = await _db.CheckInLogs.AnyAsync(c =>
                c.TicketId == entry.TicketId
                && c.Action == (byte)CheckInAction.CheckIn
                && c.IsValid
                && c.ActionTime >= rentStart
                && c.ActionTime < rentEnd
                && c.ActionTime <= now);
            if (!checkedInDuringRent) return false;

            var newVisit = new ReviewPerVisit
            {
                TicketId = entry.TicketId,
                OrderId = entry.OrderId,
                UserId = userId,
                VenueId = reservation.VenueId,
                SportTypeId = venue.SportTypeId,
                RentStartTime = rentStart,
                RentEndTime = rentEnd,
                ActualEndTime = null,
                CreatedAt = _timeService.Now,
                ExpiredAt = _timeService.Now.AddDays(ReviewPolicy.TicketValidDays)
            };

            // Add 不是 I/O，不需要 async；AddAsync 只有在用特殊主鍵產生策略時才需要。
            _db.ReviewPerVisits.Add(newVisit);

            // try/catch 的決定：上面已經查過「建過沒」，但同一位會員的兩張票幾乎同時報到時，
            // 兩邊可能都查到「還沒建」，後到的那一筆會撞到唯一約束。這不是錯誤，結果跟「已經建過」一樣，
            // 所以只接住「違反唯一約束」這一種，其他資料庫錯誤照常往外丟。
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _db.Entry(newVisit).State = EntityState.Detached;   // 不要讓這筆失敗的新增留在追蹤清單，影響之後的 SaveChanges
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> RecordVisitEndTimeAsync(int? ticketId)
        {
            if (ticketId == null) return false;

            var entry = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (entry == null) return false;
            if (entry.ReceivedUserId is not int userId) return false;   // 沒有領票的會員，就不會有憑證

            var reservation = await (from s in _db.Reservations
                                     join o in _db.Orders on s.ReservationId equals o.ReservationId
                                     where o.OrderId == entry.OrderId
                                     select s).FirstOrDefaultAsync();
            if (reservation == null) return false;

            // 刻意不分入場／離場、成功／失敗：只取這張票「最新」的一筆紀錄。
            // 呼叫時機由刷退方法決定，這裡不重做組員的進出判斷，他那邊的判斷有問題也不會牽連到這裡。
            // ActionTime 只存到秒，同一秒有兩筆時再用 LogId（流水號，後寫的比較大）決定誰是最新。
            var latestLog = await _db.CheckInLogs
                                     .Where(c => c.TicketId == ticketId && 
                                        (c.Action == (byte)CheckInAction.CheckIn || c.Action == (byte)CheckInAction.CheckOut))
                                     .OrderByDescending(c => c.ActionTime)
                                     .ThenByDescending(c => c.LogId)
                                     .FirstOrDefaultAsync();
            if (latestLog == null) return false;

            var rentStartTime = reservation.BookingDate.ToDateTime(reservation.StartTime);
            if (latestLog.ActionTime < rentStartTime) return false; // 離場時間不可能早於租借開始時間

            // 用「訂單＋會員」找憑證，不用 TicketId：
            // 同一個人拿第二張票離場，憑證掛在第一張票上，用 TicketId 會找不到。
            var visitTicket = await FindVisitTicketAsync(entry.OrderId, userId);
            // 補償：入場時沒建立評論憑證（例如票券清單的「快速報到」不會呼叫工廠），就在這裡補建一次。
            // 條件跟 CreateReviewPerVisitAsync 一樣，不符合就照舊回 false。
            // 補建的 CreatedAt／ExpiredAt 從現在起算，會比入場時建立晚一點。
            if (visitTicket == null)
            {
                if (!await CreateReviewPerVisitAsync(entry.Qrtoken)) return false;
                visitTicket = await FindVisitTicketAsync(entry.OrderId, userId);
                if (visitTicket == null) return false;
            }

            // 同一個人的多張票都會呼叫這裡：取最晚的離場時間，不讓先離場的那張蓋掉後離場的
            if (visitTicket.ActualEndTime != null && visitTicket.ActualEndTime >= latestLog.ActionTime) return true;

            visitTicket.ActualEndTime = latestLog.ActionTime;
            await _db.SaveChangesAsync();

            return true;
        }

        private Task<ReviewPerVisit?> FindVisitTicketAsync(int orderId, int userId)
            => _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.OrderId == orderId && v.UserId == userId);

        /// <summary>SQL Server 的「違反唯一索引」（2601）或「違反唯一約束」（2627）。</summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is SqlException { Number: 2601 or 2627 };

        /// <inheritdoc/>
        public async Task<bool> CreateReviewPerBookingAsync(int? orderId)
        {
            if (orderId == null) return false;

            if (await _db.ReviewPerBookings.AnyAsync(b => b.OrderId == orderId)) return false; // 防止重複建立

            var book = await (from p in _db.Payments
                              join o in _db.Orders on p.OrderId equals o.OrderId
                              where o.OrderId == orderId && p.PaidAt != null // 有付款紀錄才建立
                              select new { p, o }).FirstOrDefaultAsync();
            if (book == null) return false;

            var order = book.o;
            var payment = book.p;

            var newBooking = new ReviewPerBooking
            {
                UserId = order.UserId,
                OrderId = order.OrderId,
                PaymentMethod = payment.PaymentMethod,
                CreatedAt = _timeService.Now,
                ExpiredAt = _timeService.Now.AddDays(ReviewPolicy.TicketValidDays)
            };

            _db.ReviewPerBookings.Add(newBooking);
            await _db.SaveChangesAsync();

            return true;
        }
    }
}
