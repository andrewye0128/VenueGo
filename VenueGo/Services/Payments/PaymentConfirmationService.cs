using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.ReviewTickets;
using VenueGo.Services.Ticket;
using VenueGo.Services.Time;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 收款確認的實作。
    /// <para>
    /// 【有條件的更新】每一步都用 ExecuteUpdateAsync 加上狀態條件（同 ReservationExpiryService）。
    /// 更新到 0 筆＝剛好被別的操作搶先（另一個綠界通知、逾期排程、後台取消），
    /// 這時整筆復原，重新讀一次最新狀態再判斷，最多重來 1 次。
    /// </para>
    /// <para>
    /// 【逾期後才收到錢】（3-B 決定）時段還空著就重新佔位、恢復成已確認；
    /// 被別人訂走、或使用時段已經開始，付款改成退款處理中，由櫃檯退款（Refunds 尚未實作）。
    /// </para>
    /// </summary>
    public class PaymentConfirmationService : IPaymentConfirmationService
    {
        /// <summary>被別的操作搶先時，最多試幾次（第 1 次＋重來 1 次）</summary>
        private const int MaxAttempts = 2;

        private readonly dbVenueContext _db;
        private readonly ITimeService _timeService;
        private readonly IEntryTicketService _entryTicketService;
        private readonly IBookingReviewTicketFactory _bookingFactory;
        private readonly ILogger<PaymentConfirmationService> _logger;

        public PaymentConfirmationService(
            dbVenueContext db,
            ITimeService timeService,
            IEntryTicketService entryTicketService,
            IBookingReviewTicketFactory bookingFactory,
            ILogger<PaymentConfirmationService> logger)
        {
            _db = db;
            _timeService = timeService;
            _entryTicketService = entryTicketService;
            _bookingFactory = bookingFactory;
            _logger = logger;
        }

        public async Task<PaymentConfirmResult> ConfirmPaidAsync(
            PaymentConfirmRequest request, CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var result = await TryConfirmAsync(request, cancellationToken);
                if (result is not null) return result;

                // 被搶先了：清掉這次留在 DbContext 裡的東西，重新讀狀態再判斷一次
                _db.ChangeTracker.Clear();
            }

            return new PaymentConfirmResult(
                PaymentConfirmOutcome.Conflict, "付款狀態剛被其他操作變更，請重新整理後再試。");
        }

        /// <summary>處理需要的資料（只讀，不追蹤）</summary>
        private sealed record Target(
            int PaymentId, int Amount, byte PaymentStatus,
            int OrderId, string OrderNo, byte OrderStatus,
            int ReservationId, byte ReservationStatus,
            int VenueId, DateOnly BookingDate, TimeOnly StartTime);

        /// <returns>null＝被別的操作搶先，要重來</returns>
        private async Task<PaymentConfirmResult?> TryConfirmAsync(PaymentConfirmRequest req, CancellationToken ct)
        {
            var t = await (from p in _db.Payments.AsNoTracking()
                           join o in _db.Orders.AsNoTracking() on p.OrderId equals o.OrderId
                           // Orders.ReservationId 是 int?，要轉型才能跟 int 比對
                           join r in _db.Reservations.AsNoTracking() on o.ReservationId equals (int?)r.ReservationId
                           where p.PaymentId == req.PaymentId
                           select new Target(
                               p.PaymentId, p.Amount, p.PaymentStatus,
                               o.OrderId, o.OrderNo, o.OrderStatus,
                               r.ReservationId, r.ReservationStatus,
                               r.VenueId, r.BookingDate, r.StartTime))
                          .FirstOrDefaultAsync(ct);

            if (t is null)
            {
                return new PaymentConfirmResult(PaymentConfirmOutcome.NotFound, "查無此付款紀錄。");
            }

            // 已經記錄過這筆錢：重複的通知走這裡，什麼都不改
            if (IsMoneyAlreadyRecorded(t.PaymentStatus))
            {
                return new PaymentConfirmResult(
                    PaymentConfirmOutcome.AlreadyProcessed, "此訂單已是已付款狀態。", t.OrderNo);
            }

            if (req.PaidAmount is int paid && paid != t.Amount)
            {
                _logger.LogWarning("訂單 {OrderNo} 付款金額不符：收到 {Paid}，訂單 {Amount}", t.OrderNo, paid, t.Amount);
                return new PaymentConfirmResult(
                    PaymentConfirmOutcome.AmountMismatch,
                    $"付款金額 {paid:N0} 與訂單金額 {t.Amount:N0} 不符。", t.OrderNo);
            }

            var now = _timeService.Now;

            if (t.ReservationStatus is (byte)ReservationStatus.Pending or (byte)ReservationStatus.Confirmed)
            {
                return await ConfirmAsync(t, req, now, ct);
            }

            // 以下是預約已經失效。後台不能標記；綠界的錢已經收到了，一定要記下來
            if (req.Source == PaymentConfirmSource.Staff)
            {
                return new PaymentConfirmResult(
                    PaymentConfirmOutcome.NotPayable, "此預約已結束或已取消，無法變更付款狀態。", t.OrderNo);
            }

            return t.ReservationStatus == (byte)ReservationStatus.Expired
                ? await ConfirmAfterExpiryAsync(t, req, now, ct)
                : await MarkRefundingAsync(t, req, now, "預約已取消", ct);
        }

        // ══ 一般收款 ═══════════════════════════════

        private async Task<PaymentConfirmResult?> ConfirmAsync(
            Target t, PaymentConfirmRequest req, DateTime now, CancellationToken ct)
        {
            // 沒有 CommitAsync 就離開這個方法時，交易會自動 Rollback，前面改的全部復原
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // 1. 付款：還沒記錄過這筆錢才改
            if (await UpdatePaymentAsync(t.PaymentId, PaymentStatus.Paid, req, now, ct) == 0) return null;

            // 2. 預約：還是待確認／已確認才改（逾期排程可能剛好搶先改成已逾期）
            var reservationUpdated = await _db.Reservations
                .Where(r => r.ReservationId == t.ReservationId
                         && (r.ReservationStatus == (byte)ReservationStatus.Pending
                             || r.ReservationStatus == (byte)ReservationStatus.Confirmed))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservationStatus, (byte)ReservationStatus.Confirmed), ct);
            if (reservationUpdated == 0) return null;

            // 3. 訂單
            await UpdateOrderPaidAsync(t.OrderId, ct);

            // 4. 操作紀錄
            AddAuditLog(t, req, AuditActions.MarkOrderAsPaid,
                (byte)ReservationStatus.Confirmed, (byte)OrderStatus.Paid, (byte)PaymentStatus.Paid, now);
            await _db.SaveChangesAsync(ct);

            // 5. 入場票券。建立失敗整筆不算數（不 Commit，上面改的全部復原），同原本的 MarkAsPaidAsync
            var ticket = await _entryTicketService.CreateForOrderAsync(t.OrderId);
            if (ticket.TicketCount == 0)
            {
                return new PaymentConfirmResult(PaymentConfirmOutcome.TicketFailed, ticket.Message, t.OrderNo);
            }

            await transaction.CommitAsync(ct);
            await AfterPaidAsync(t.OrderId);

            return new PaymentConfirmResult(
                PaymentConfirmOutcome.Confirmed, "已收款，預約已確認。", t.OrderNo, ticket.TicketCount);
        }

        // ══ 逾期後才收到錢 ═══════════════════════════════

        private async Task<PaymentConfirmResult?> ConfirmAfterExpiryAsync(
            Target t, PaymentConfirmRequest req, DateTime now, CancellationToken ct)
        {
            // 使用時段已經開始：就算時段空著，重新佔位也沒有意義
            if (t.BookingDate.ToDateTime(t.StartTime) <= now)
            {
                return await MarkRefundingAsync(t, req, now, "使用時段已開始", ct);
            }

            // 要重新佔的時段：從訂單明細拿（建立預約時，佔位資料也是一格明細一列）
            var slotTimes = await _db.OrdersDetails.AsNoTracking()
                .Where(d => d.OrderId == t.OrderId && d.SlotTime != null)
                .Select(d => d.SlotTime!.Value)
                .ToListAsync(ct);
            if (slotTimes.Count == 0)
            {
                return await MarkRefundingAsync(t, req, now, "查無預約時段", ct);
            }

            int ticketCount;
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                // 1. 付款（逾期時被改成付款取消，這裡改回已付款）
                if (await UpdatePaymentAsync(t.PaymentId, PaymentStatus.Paid, req, now, ct) == 0) return null;

                // 2. 預約：還是已逾期才改，並清掉逾期時寫入的取消資訊
                var reservationUpdated = await _db.Reservations
                    .Where(r => r.ReservationId == t.ReservationId
                             && r.ReservationStatus == (byte)ReservationStatus.Expired)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(r => r.ReservationStatus, (byte)ReservationStatus.Confirmed)
                        .SetProperty(r => r.CancelledBy, (int?)null)
                        .SetProperty(r => r.CancelledAt, (DateTime?)null)
                        .SetProperty(r => r.CancelReason, (string?)null), ct);
                if (reservationUpdated == 0) return null;

                // 3. 訂單
                await UpdateOrderPaidAsync(t.OrderId, ct);

                // 4. 重新佔位。時段被別人訂走的話，唯一鍵 UQ_ReservationSlots_Occupancy 會在 SaveChanges 時擋下來
                _db.ReservationSlots.AddRange(slotTimes.Select(slotTime => new ReservationSlot
                {
                    ReservationId = t.ReservationId,
                    VenueId = t.VenueId,
                    BookingDate = t.BookingDate,
                    SlotTime = slotTime
                }));

                // 5. 操作紀錄
                AddAuditLog(t, req, AuditActions.ConfirmLatePayment,
                    (byte)ReservationStatus.Confirmed, (byte)OrderStatus.Paid, (byte)PaymentStatus.Paid, now);
                await _db.SaveChangesAsync(ct);

                // 6. 入場票券
                var ticket = await _entryTicketService.CreateForOrderAsync(t.OrderId);
                if (ticket.TicketCount == 0)
                {
                    return new PaymentConfirmResult(PaymentConfirmOutcome.TicketFailed, ticket.Message, t.OrderNo);
                }
                ticketCount = ticket.TicketCount;

                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // 時段被別人訂走了。離開 using 時交易已經復原，上面 1～3 步都沒有生效
                _db.ChangeTracker.Clear();
                return await MarkRefundingAsync(t, req, now, "時段已被其他人預約", ct);
            }

            _logger.LogInformation("訂單 {OrderNo} 逾期後才收到付款，時段還空著，已重新佔位", t.OrderNo);
            await AfterPaidAsync(t.OrderId);

            return new PaymentConfirmResult(
                PaymentConfirmOutcome.ReoccupiedAfterExpiry,
                "已收款。預約原本已逾期，時段還空著，已恢復成已確認。", t.OrderNo, ticketCount);
        }

        // ══ 預約已失效，錢要退回去 ═══════════════════════════════

        private async Task<PaymentConfirmResult?> MarkRefundingAsync(
            Target t, PaymentConfirmRequest req, DateTime now, string reason, CancellationToken ct)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // 只改付款。預約、訂單維持原本的已逾期／已取消，時段也不佔
            if (await UpdatePaymentAsync(t.PaymentId, PaymentStatus.Refunding, req, now, ct) == 0) return null;

            AddAuditLog(t, req, AuditActions.RefundLatePayment,
                t.ReservationStatus, t.OrderStatus, (byte)PaymentStatus.Refunding, now);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // 用 Warning：這筆錢要有人去退，log 裡要看得到
            _logger.LogWarning("訂單 {OrderNo} 在預約失效後才收到付款（{Reason}），已改成退款處理中", t.OrderNo, reason);

            return new PaymentConfirmResult(
                PaymentConfirmOutcome.RefundingAfterExpiry,
                $"已收款，但預約已失效（{reason}），付款已改成退款處理中，請到櫃檯辦理退款。", t.OrderNo);
        }

        // ── 共用的小工具 ────────────────────────────────

        /// <summary>已付款、退款處理中、已退款＝這筆錢已經記錄過了</summary>
        private static bool IsMoneyAlreadyRecorded(byte paymentStatus)
            => paymentStatus is (byte)PaymentStatus.Paid
                or (byte)PaymentStatus.Refunding
                or (byte)PaymentStatus.Refunded;

        /// <summary>
        /// 付款改成已付款或退款處理中。條件：這筆錢還沒記錄過（未付款、付款處理中、付款失敗、付款取消）。
        /// 付款取消也要接受：逾期、後台取消時會改成付款取消，之後綠界才通知付款成功
        /// </summary>
        /// <returns>更新到幾筆；0＝被搶先了</returns>
        private Task<int> UpdatePaymentAsync(
            int paymentId, PaymentStatus newStatus, PaymentConfirmRequest req, DateTime now, CancellationToken ct)
        {
            var transactionNo = req.TransactionNo;

            return _db.Payments
                .Where(p => p.PaymentId == paymentId
                         && p.PaymentStatus != (byte)PaymentStatus.Paid
                         && p.PaymentStatus != (byte)PaymentStatus.Refunding
                         && p.PaymentStatus != (byte)PaymentStatus.Refunded)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PaymentStatus, (byte)newStatus)
                    .SetProperty(p => p.PaidAt, (DateTime?)now)   // 網站時間（3-C 決定），綠界的真實時間記在 PaymentTransactions
                                                                  // 後台沒有交易編號，就保留原本的值
                    .SetProperty(p => p.TransactionNo, p => transactionNo ?? p.TransactionNo), ct);
        }

        private Task<int> UpdateOrderPaidAsync(int orderId, CancellationToken ct)
            => _db.Orders
                .Where(o => o.OrderId == orderId)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, (byte)OrderStatus.Paid), ct);

        /// <summary>收款後、交易成功之後才做的事（失敗也不影響收款）</summary>
        private Task AfterPaidAsync(int orderId)
        {
            // 評論憑證：暫時關閉（2026-10-23 使用者決定；F5 的 CreateReviewPerBookingAsync 目前也是空的）
            // 要打開時：把下面這行的註解拿掉，並把 return Task.CompletedTask 刪掉
            //return _bookingFactory.CreateReviewPerBookingAsync(orderId);
            return Task.CompletedTask;
        }

        /// <summary>操作紀錄。格式同 ReservationCommandService：只記三個狀態，主體是 Reservations</summary>
        private void AddAuditLog(
            Target t, PaymentConfirmRequest req, string action,
            byte newReservationStatus, byte newOrderStatus, byte newPaymentStatus, DateTime now)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = req.OperatorUserId,   // 綠界是 NULL（不是人操作）
                Action = action,
                EntityType = AuditEntityTypes.Reservations,
                EntityId = t.ReservationId.ToString(),
                OldValue = SerializeStatuses(t.ReservationStatus, t.OrderStatus, t.PaymentStatus),
                NewValue = SerializeStatuses(newReservationStatus, newOrderStatus, newPaymentStatus),
                CreatedAt = now
            });
        }

        private static string SerializeStatuses(byte reservationStatus, byte orderStatus, byte paymentStatus)
            => JsonSerializer.Serialize(new
            {
                ReservationStatus = reservationStatus,
                OrderStatus = orderStatus,
                PaymentStatus = paymentStatus
            });

        /// <summary>唯一鍵衝突（2601 唯一索引、2627 UNIQUE 約束），同 ReservationCreationService</summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is SqlException sql
               && (sql.Number == 2601 || sql.Number == 2627);
    }
}