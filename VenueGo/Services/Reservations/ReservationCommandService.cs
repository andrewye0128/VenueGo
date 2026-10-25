using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.PaymentModels;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.Payments;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約狀態異動服務的實作。
    /// </summary>
    public class ReservationCommandService : IReservationCommandService
    {
        /// <summary>取消原因的長度上限，與 Reservations.CancelReason 的 nvarchar(200) 一致。</summary>
        private const int MaxReasonLength = 200;

        private readonly dbVenueContext _db;
        private readonly IPaymentConfirmationService _paymentConfirmation;
        private readonly ITimeService _timeService;

        public ReservationCommandService(
            dbVenueContext db,
            IPaymentConfirmationService paymentConfirmation,
            ITimeService timeService)
        {
            _db = db;

            // 收款確認（改狀態、建票券、評論憑證）跟綠界共用，放在 PaymentConfirmationService
            _paymentConfirmation = paymentConfirmation;

            _timeService = timeService;
        }

        // ══ 標記為已付款 ═══════════════════════════════

        public async Task<ReservationCommandResult> MarkAsPaidAsync(
            int reservationId, int operatorUserId, CancellationToken cancellationToken = default)
        {
            // 前面的檢查留在這裡：錯誤訊息是給後台員工看的，跟原本一樣
            var reservation = await FindReservationAsync(reservationId, cancellationToken);
            if (reservation is null)
            {
                return ReservationCommandResult.Fail("查無此預約。");
            }

            if (!IsActive(reservation.ReservationStatus))
            {
                return ReservationCommandResult.Fail(
                    "此預約已結束或已取消，無法變更付款狀態。");
            }

            var order = await FindOrderAsync(reservationId, cancellationToken);
            if (order is null)
            {
                return ReservationCommandResult.Fail("此預約查無對應的訂單，請聯絡系統管理員。");
            }

            var payment = await FindPaymentAsync(order.OrderId, cancellationToken);
            if (payment is null)
            {
                return ReservationCommandResult.Fail("此訂單查無付款紀錄，請聯絡系統管理員。");
            }

            if (payment.PaymentStatus == (byte)PaymentStatus.Paid)
            {
                return ReservationCommandResult.Fail("此訂單已是已付款狀態。");
            }

            // 改狀態、建票券、操作紀錄交給共用的收款確認（綠界付款成功也走同一段）
            var result = await _paymentConfirmation.ConfirmPaidAsync(
                new PaymentConfirmRequest(payment.PaymentId, PaymentConfirmSource.Staff, operatorUserId),
                cancellationToken);

            if (result.Outcome != PaymentConfirmOutcome.Confirmed)
            {
                return ReservationCommandResult.Fail(result.Message);
            }

            return ReservationCommandResult.Success(
                $"已將訂單 {order.OrderNo} 標記為已付款，預約狀態更新為已確認，已產生 {result.TicketCount} 張票券。");
        }

        // ══ 取消與作廢 ════════════════════════════════

        public Task<ReservationCommandResult> CancelAsync(
            int reservationId, string reason, int operatorUserId,
            CancellationToken cancellationToken = default)
            => TerminateAsync(
                reservationId, reason, operatorUserId,
                ReservationStatus.Cancelled,
                AuditActions.CancelReservation,
                "已取消此預約，時段已釋放。",
                cancellationToken);

        public Task<ReservationCommandResult> VoidAsync(
            int reservationId, string reason, int operatorUserId,
            CancellationToken cancellationToken = default)
            => TerminateAsync(
                reservationId, reason, operatorUserId,
                ReservationStatus.Voided,
                AuditActions.VoidReservation,
                "已作廢此預約，時段已釋放，且不計入營運統計。",
                cancellationToken);

        /// <summary>
        /// 終止預約的共用流程。取消與作廢的處理完全相同，
        /// 差別只在寫入的狀態值、稽核動作代碼與成功訊息，因此抽成同一個方法。
        /// </summary>
        private async Task<ReservationCommandResult> TerminateAsync(
            int reservationId,
            string reason,
            int operatorUserId,
            ReservationStatus targetStatus,
            string auditAction,
            string successMessage,
            CancellationToken cancellationToken)
        {
            var trimmedReason = reason?.Trim();
            if (string.IsNullOrEmpty(trimmedReason))
            {
                return ReservationCommandResult.Fail("請填寫原因。");
            }

            if (trimmedReason.Length > MaxReasonLength)
            {
                return ReservationCommandResult.Fail(
                    $"原因請勿超過 {MaxReasonLength} 個字。");
            }

            var reservation = await FindReservationAsync(reservationId, cancellationToken);
            if (reservation is null)
            {
                return ReservationCommandResult.Fail("查無此預約。");
            }

            if (!IsActive(reservation.ReservationStatus))
            {
                return ReservationCommandResult.Fail("此預約已結束或已取消，無需重複操作。");
            }

            var order = await FindOrderAsync(reservationId, cancellationToken);
            var payment = order is null
                ? null
                : await FindPaymentAsync(order.OrderId, cancellationToken);

            var oldValue = SerializeStatuses(
                reservation.ReservationStatus, order?.OrderStatus, payment?.PaymentStatus);

            var requiresRefund = payment?.PaymentStatus == (byte)PaymentStatus.Paid;

            var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                var now = _timeService.Now;

                reservation.ReservationStatus = (byte)targetStatus;
                reservation.CancelledBy = operatorUserId;
                reservation.CancelledAt = now;
                reservation.CancelReason = trimmedReason;

                // 這一步是整個流程最關鍵的部分。
                // UQ_ReservationSlots_Occupancy 唯一鍵不看預約狀態，
                // 只要這幾列還在，該時段就永遠無法被再次預約。
                // 歷史資料不會遺失，因為 Reservations 本身已存有
                // VenueId / BookingDate / StartTime / EndTime。
                await ReleaseSlotsAsync(reservationId, cancellationToken);

                if (order is not null)
                {
                    order.OrderStatus = (byte)OrderStatus.Cancelled;
                }

                if (payment is not null)
                {
                    // 已付款 → 錢要退回去，轉為退款處理中，由退款流程接手
                    // 未付款 → 這筆錢不用收了，轉為付款取消
                    //
                    // 未付款時不可停留在 Unpaid：期末的逾期排程會撈所有 Unpaid 的紀錄，
                    // 已取消的單留在該狀態會被反覆處理。
                    payment.PaymentStatus = requiresRefund
                        ? (byte)PaymentStatus.Refunding
                        : (byte)PaymentStatus.Cancelled;
                }

                var newValue = SerializeStatuses(
                    reservation.ReservationStatus, order?.OrderStatus, payment?.PaymentStatus);

                AddAuditLog(reservationId, operatorUserId, auditAction, oldValue, newValue, now);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            // 已收款者要提醒櫃檯處理退款，否則那筆錢會卡在系統裡沒人管。
            // 期中 Refunds 尚未實作，退款由櫃檯線下處理，系統只負責記錄狀態。
            if (requiresRefund)
            {
                var amountText = payment is null ? string.Empty : $" NT$ {payment.Amount:N0}";
                return ReservationCommandResult.Success(
                    $"{successMessage}此預約已收款{amountText}，請於櫃檯辦理退款。");
            }

            return ReservationCommandResult.Success(successMessage);
        }

        // ── 共用的小工具 ────────────────────────────────

        /// <summary>
        /// 預約是否仍在有效狀態。
        /// 只有待確認與已確認兩種狀態下時段仍被佔用，才允許後續操作。
        /// </summary>
        private static bool IsActive(byte reservationStatus)
            => reservationStatus == (byte)ReservationStatus.Pending
               || reservationStatus == (byte)ReservationStatus.Confirmed;

        /// <summary>
        /// 這裡不加 AsNoTracking，因為取出的物件要被修改後存回資料庫。
        /// </summary>
        private Task<Reservation?> FindReservationAsync(int reservationId, CancellationToken ct)
            => _db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct);

        private Task<Order?> FindOrderAsync(int reservationId, CancellationToken ct)
            => _db.Orders.FirstOrDefaultAsync(o => o.ReservationId == reservationId, ct);

        /// <summary>
        /// 取最新一筆付款（CreatedAt 最晚），跟「我的預約」「訂單與付款」、逾期排程同一個規則。
        /// 原本取最舊的一筆：一筆訂單有多筆付款時，會改到舊的那筆
        /// </summary>
        private Task<Payment?> FindPaymentAsync(int orderId, CancellationToken ct)
            => _db.Payments
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PaymentId)
                .FirstOrDefaultAsync(ct);

        /// <summary>刪除該筆預約的所有佔位資料，釋放時段。</summary>
        private async Task ReleaseSlotsAsync(int reservationId, CancellationToken ct)
        {
            var slots = await _db.ReservationSlots
                .Where(s => s.ReservationId == reservationId)
                .ToListAsync(ct);

            if (slots.Count == 0) return;

            _db.ReservationSlots.RemoveRange(slots);
        }

        /// <summary>
        /// 把三個狀態序列化成簡短的 JSON，供稽核紀錄的 OldValue / NewValue 使用。
        /// <para>
        /// 只記有變動的狀態欄位，不把整筆資料塞進去：
        /// 那兩個欄位是 nvarchar(500)，整筆資料會塞不下，
        /// 而且之後要看「改了什麼」還得自己比對。
        /// </para>
        /// </summary>
        private static string SerializeStatuses(
            byte reservationStatus, byte? orderStatus, byte? paymentStatus)
        {
            return JsonSerializer.Serialize(new
            {
                ReservationStatus = reservationStatus,
                OrderStatus = orderStatus,
                PaymentStatus = paymentStatus
            });
        }

        /// <summary>
        /// 新增一筆稽核紀錄。
        /// <para>
        /// 一次操作改了三張表，但只寫一筆紀錄，EntityType 填主體 Reservations。
        /// 因為那三個狀態的變動是同一個業務動作；
        /// 拆成三筆之後要查「這次取消改了什麼」還得自己拼回來。
        /// </para>
        /// </summary>
        private void AddAuditLog(
            int reservationId, int operatorUserId,
            string action, string oldValue, string newValue, DateTime now)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = operatorUserId,
                Action = action,
                EntityType = AuditEntityTypes.Reservations,
                EntityId = reservationId.ToString(),
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = now
            });
        }
    }
}