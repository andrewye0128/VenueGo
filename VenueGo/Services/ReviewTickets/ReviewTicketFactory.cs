using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.ReviewTickets
{
    // 一個實作、兩個門：報到系統看到 IVisitReviewTicketFactory，
    // 訂單／付款系統看到 IBookingReviewTicketFactory，各自只看得到自己該叫的方法。
    // 10/11 加第三個門：「我的評論」清單看到 IReviewTicketBackfill（補建漏掉的憑證），別組看不到。
    //
    // 文件註解寫在「介面」上，不寫在這裡：呼叫端拿到的是介面，
    // IntelliSense 顯示的也是介面上的註解。<inheritdoc/> 讓這邊直接沿用，
    // 不會出現兩份說明各說各話的情況。
    public sealed class ReviewTicketFactory(dbVenueContext db, ITimeService timeService)
        : IVisitReviewTicketFactory, IBookingReviewTicketFactory, IReviewTicketBackfill
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
        //    5. 評論期限＝預約時段內「最早一筆」有效入場紀錄的時間＋14 天，不是建立憑證的時間＋14 天；已經過期就不建（10/11）。
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

            // 原本（10/10 RPV 修正版；10/11 改成取「最早一筆」的時間，期限從那時起算）：
            // // 規則 3：預約時段內有一筆成功的入場紀錄，而且這筆紀錄已經發生了（不晚於現在）
            // bool checkedInDuringRent = await _db.CheckInLogs.AnyAsync(c =>
            //     c.TicketId == entry.TicketId
            //     && c.Action == (byte)CheckInAction.CheckIn
            //     && c.IsValid
            //     && c.ActionTime >= rentStart
            //     && c.ActionTime < rentEnd
            //     && c.ActionTime <= now);
            // if (!checkedInDuringRent) return false;

            // 規則 3：預約時段內有一筆成功的入場紀錄，而且這筆紀錄已經發生了（不晚於現在）。
            // 10/11：不只問「有沒有」，還取「最早一筆」的時間，規則 5 的期限從這個時間起算。
            //   MinAsync 要寫成 DateTime?：一筆都沒有時回 null（寫成 DateTime 的話，沒有資料會丟例外）。
            DateTime? firstCheckInAt = await _db.CheckInLogs
                .Where(c => c.TicketId == entry.TicketId
                            && c.Action == (byte)CheckInAction.CheckIn
                            && c.IsValid
                            && c.ActionTime >= rentStart
                            && c.ActionTime < rentEnd
                            && c.ActionTime <= now)
                .MinAsync(c => (DateTime?)c.ActionTime);
            if (firstCheckInAt is not DateTime checkInAt) return false;

            // 規則 5（10/11 新增）：評論期限從「報到時間」起算，不是從「建立憑證的時間」。
            //   正常流程是報到當下就建立，兩種算法結果一樣。差別在「晚補建」（離場時補、「我的評論」清單補）：
            //   從建立時間算的話，晚補建的憑證期限會被拉長，早就該過期的到場突然又能評。
            //   期限已經過了就不建：CHK_ReviewPerVisit_Time_Logic 要求 ExpiredAt > CreatedAt，硬建會撞約束。
            DateTime expiredAt = checkInAt.AddDays(ReviewPolicy.TicketValidDays);
            if (expiredAt <= now) return false;

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
                CreatedAt = now,                 // 跟上面的判斷用同一個時間
                ExpiredAt = expiredAt            // 規則 5：報到時間＋14 天
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
            // 10/11：補建的 CreatedAt 是現在；ExpiredAt 從報到時間起算（規則 5），所以期限跟入場時就建立的一樣。
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

            // 10/11：取「最早一筆」已付款紀錄，期限從付款時間起算（跟現場憑證的規則 5 同一個道理）。
            //   付款時間晚於現在的不算（時光機撥回過去時會遇到），跟現場憑證「報到紀錄不能晚於現在」一致。
            DateTime now = _timeService.Now;
            var book = await (from p in _db.Payments
                              join o in _db.Orders on p.OrderId equals o.OrderId
                              where o.OrderId == orderId && p.PaidAt != null && p.PaidAt <= now // 有付款紀錄才建立
                              orderby p.PaidAt, p.PaymentId
                              select new { p, o }).FirstOrDefaultAsync();
            if (book == null) return false;
            if (book.p.PaidAt is not DateTime paidAt) return false;   // 查詢已經排除 null，這行是讓編譯器知道它有值

            var order = book.o;
            var payment = book.p;

            // 期限已經過了就不建：CHK_ReviewPerBooking_Time_Logic 要求 ExpiredAt > CreatedAt，硬建會撞約束。
            DateTime expiredAt = paidAt.AddDays(ReviewPolicy.TicketValidDays);
            if (expiredAt <= now) return false;

            var newBooking = new ReviewPerBooking
            {
                UserId = order.UserId,
                OrderId = order.OrderId,
                PaymentMethod = payment.PaymentMethod,
                CreatedAt = now,
                ExpiredAt = expiredAt            // 付款時間＋14 天
            };

            _db.ReviewPerBookings.Add(newBooking);
            await _db.SaveChangesAsync();

            return true;
        }

        // ── 待評論清單的補建（10/11，方案 A）──────────────────────────
        //
        //  憑證本來只靠別組在報到、離場、付款時呼叫上面的方法建立；漏呼叫（例如快速報到）就沒有憑證，
        //  會員在「我的評論」也看不到。所以清單打開時，先替本人補建一次。
        //  條件全部交給 CreateReviewPerVisitAsync／CreateReviewPerBookingAsync 判斷（含防止重複），
        //  這裡只負責找出「可能缺憑證」的候選，不重寫規則。
        //
        //  候選只找最近 14 天（TicketValidDays）內的：期限從報到／付款時間起算，更早的一定已經過期，
        //  找出來也建不成，只會讓每次打開清單都白跑一輪查詢。

        /// <inheritdoc/>
        public async Task<int> BackfillVisitTicketsAsync(int userId)
        {
            DateTime now = _timeService.Now;

            // 報到紀錄一定在預約時段內，所以「預約日期早於 14 天前那一天」的票，報到時間＋14 天一定已經過了。
            // 用日期比較、不用「日期＋時間」：DateOnly 加 TimeOnly 的組合不一定翻得成 SQL。
            // 這樣會多抓到「14 天前那一天」的票，交給工廠擋掉就好，不會漏掉任何一張。
            DateOnly earliestBookingDate = DateOnly.FromDateTime(now.AddDays(-ReviewPolicy.TicketValidDays));

            var tokens = await (from t in _db.EntryTickets
                                join o in _db.Orders on t.OrderId equals o.OrderId
                                join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                where t.ReceivedUserId == userId
                                      && (t.Status == (byte)EntryTicketStatus.Used || t.Status == (byte)EntryTicketStatus.Completed)
                                      && r.BookingDate >= earliestBookingDate
                                      && !_db.ReviewPerVisits.Any(v => v.UserId == userId && v.OrderId == t.OrderId)
                                select t.Qrtoken)
                               .ToListAsync();

            int created = 0;
            foreach (string token in tokens)
            {
                // 同一位會員在同一張訂單有兩張票時，兩張都會是候選；第一張建好之後，第二張會被規則 2 擋下（回 false）
                if (await CreateReviewPerVisitAsync(token)) created++;
            }
            return created;
        }

        /// <inheritdoc/>
        public async Task<int> BackfillBookingTicketsAsync(int userId)
        {
            DateTime now = _timeService.Now;
            DateTime earliestPaidAt = now.AddDays(-ReviewPolicy.TicketValidDays);   // 付款時間＋14 天 > 現在 ⇔ 付款時間 > 現在－14 天

            var orderIds = await _db.Orders
                .Where(o => o.UserId == userId
                            && !_db.ReviewPerBookings.Any(b => b.OrderId == o.OrderId)
                            && _db.Payments.Any(p => p.OrderId == o.OrderId
                                                     && p.PaidAt != null
                                                     && p.PaidAt > earliestPaidAt
                                                     && p.PaidAt <= now))
                .Select(o => o.OrderId)
                .ToListAsync();

            int created = 0;
            foreach (int orderId in orderIds)
            {
                if (await CreateReviewPerBookingAsync(orderId)) created++;
            }
            return created;
        }
    }
}
