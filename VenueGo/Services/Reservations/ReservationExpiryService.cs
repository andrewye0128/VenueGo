using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約逾期處理的實作。
    /// <para>
    /// 【處理範圍】預約「待確認」＋最新一筆付款是「未付款」或「付款失敗」＋付款期限 &lt;= 現在。
    /// 線上、現場、後台代客都算，期限一到就逾期，不另外寬限。
    /// 「付款處理中」（客人正在綠界付款頁）先跳過，3-C 再改成先問綠界。
    /// </para>
    /// <para>
    /// 【不用排除後台補登單】ReservationStatus.Expired 的註解提醒補登單的期限會在過去，
    /// 但目前 AdminAllowPastDays = 0（不能補登），所以沒有這種資料。日後開放補登時要回來加條件。
    /// </para>
    /// <para>
    /// 【一筆一筆處理】每筆用自己的交易：其中一筆失敗，不會影響其他筆，下一輪會再試。
    /// </para>
    /// </summary>
    public class ReservationExpiryService : IReservationExpiryService
    {
        /// <summary>每一輪最多處理幾筆，避免累積很多時一次跑太久。</summary>
        private const int BatchSize = 100;

        /// <summary>寫入 Reservations.CancelReason 的固定文字（nvarchar 200）。</summary>
        private const string ExpireReason = "逾期未付款，系統自動取消";

        private readonly dbVenueContext _db;
        private readonly ITimeService _timeService;
        private readonly ILogger<ReservationExpiryService> _logger;

        public ReservationExpiryService(
            dbVenueContext db,
            ITimeService timeService,
            ILogger<ReservationExpiryService> logger)
        {
            _db = db;
            _timeService = timeService;
            _logger = logger;
        }

        public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
        {
            // 這一輪都用同一個「現在」：查詢和更新的條件才會一致
            var now = _timeService.Now;

            var candidates = await FindDueAsync(now, cancellationToken);

            var expiredCount = 0;
            foreach (var candidate in candidates)
            {
                try
                {
                    if (await ExpireOneAsync(candidate, now, cancellationToken))
                    {
                        expiredCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;   // 關站：往上傳，讓背景服務正常結束
                }
                catch (Exception ex)
                {
                    // 一筆失敗不影響其他筆；交易沒有 Commit，這一筆的修改會全部復原，下一輪再試
                    _logger.LogError(ex, "預約 {ReservationId} 逾期處理失敗，下一輪再試", candidate.ReservationId);

                    // 清掉這一筆留在 DbContext 裡、還沒存成功的操作紀錄，避免下一筆 SaveChanges 時又一起送出
                    _db.ChangeTracker.Clear();
                }
            }

            return expiredCount;
        }

        /// <summary>一筆要逾期的預約：處理時需要的 Id，以及寫操作紀錄用的「改之前的狀態」。</summary>
        private sealed record DueCandidate(
            int ReservationId, int OrderId, byte OrderStatus, int PaymentId, byte PaymentStatus);

        /// <summary>
        /// 找出要逾期的預約，期限最早的先處理。
        /// <para>只查資料、不修改，所以用 AsNoTracking。</para>
        /// <para>付款取最新一筆（CreatedAt 最晚），跟「我的預約」「訂單與付款」同一個規則。</para>
        /// </summary>
        private Task<List<DueCandidate>> FindDueAsync(DateTime now, CancellationToken ct)
        {
            return (from r in _db.Reservations.AsNoTracking()
                    // Orders.ReservationId 是 int?，要轉型才能跟 int 比對
                    join o in _db.Orders on (int?)r.ReservationId equals o.ReservationId
                    let payment = _db.Payments
                        .Where(p => p.OrderId == o.OrderId)
                        .OrderByDescending(p => p.CreatedAt)
                        .ThenByDescending(p => p.PaymentId)
                        .FirstOrDefault()
                    where r.ReservationStatus == (byte)ReservationStatus.Pending
                       && payment != null
                       && (payment.PaymentStatus == (byte)PaymentStatus.Unpaid
                           || payment.PaymentStatus == (byte)PaymentStatus.Failed)
                       && payment.PaymentDueAt <= now
                    orderby payment.PaymentDueAt
                    select new DueCandidate(
                        r.ReservationId, o.OrderId, o.OrderStatus, payment.PaymentId, payment.PaymentStatus))
                   .Take(BatchSize)
                   .ToListAsync(ct);
        }

        /// <summary>
        /// 把一筆預約改成逾期。四張表在同一個交易：全部成功才算數。
        /// <para>
        /// 【有條件的更新】查詢完到這裡之間，客人可能剛好付款成功。所以更新時再檢查一次
        /// 「還是沒付錢、還是待確認」，更新到 0 筆就代表狀態已經變了，整筆放棄。
        /// </para>
        /// <para>
        /// 用 ExecuteUpdateAsync：直接送一句 UPDATE ... WHERE 到資料庫，
        /// 「檢查」和「修改」在同一句 SQL 裡完成，中間不會被別人插隊。
        /// </para>
        /// </summary>
        /// <returns>true＝已改成逾期；false＝狀態已經變了（例如剛付款），沒有修改。</returns>
        private async Task<bool> ExpireOneAsync(DueCandidate c, DateTime now, CancellationToken ct)
        {
            // 沒有 CommitAsync 就離開這個方法時，交易會自動 Rollback，前面改的全部復原
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // 1. 付款：還是未付款／付款失敗、期限也真的過了，才改成付款取消
            var paymentUpdated = await _db.Payments
                .Where(p => p.PaymentId == c.PaymentId
                         && (p.PaymentStatus == (byte)PaymentStatus.Unpaid
                             || p.PaymentStatus == (byte)PaymentStatus.Failed)
                         && p.PaymentDueAt <= now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PaymentStatus, (byte)PaymentStatus.Cancelled), ct);
            if (paymentUpdated == 0) return false;

            // 2. 預約：還是待確認才改。不是人按的，所以 CancelledBy 是 NULL
            var reservationUpdated = await _db.Reservations
                .Where(r => r.ReservationId == c.ReservationId
                         && r.ReservationStatus == (byte)ReservationStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.ReservationStatus, (byte)ReservationStatus.Expired)
                    .SetProperty(r => r.CancelledBy, (int?)null)
                    .SetProperty(r => r.CancelledAt, (DateTime?)now)
                    .SetProperty(r => r.CancelReason, ExpireReason), ct);
            if (reservationUpdated == 0) return false;

            // 3. 訂單：已逾期
            await _db.Orders
                .Where(o => o.OrderId == c.OrderId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.OrderStatus, (byte)OrderStatus.Expired), ct);

            // 4. 釋放時段（最重要的一步）：佔位資料還在的話，這個時段永遠沒辦法再被預約
            await _db.ReservationSlots
                .Where(s => s.ReservationId == c.ReservationId)
                .ExecuteDeleteAsync(ct);

            // 5. 操作紀錄：格式跟後台取消（ReservationCommandService）一樣，只記三個狀態
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = null,   // 系統排程，不是人操作
                Action = AuditActions.ExpireReservation,
                EntityType = AuditEntityTypes.Reservations,
                EntityId = c.ReservationId.ToString(),
                OldValue = SerializeStatuses(
                    (byte)ReservationStatus.Pending, c.OrderStatus, c.PaymentStatus),
                NewValue = SerializeStatuses(
                    (byte)ReservationStatus.Expired, (byte)OrderStatus.Expired, (byte)PaymentStatus.Cancelled),
                CreatedAt = now
            });
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return true;
        }

        /// <summary>三個狀態轉成 JSON，格式同 ReservationCommandService.SerializeStatuses。</summary>
        private static string SerializeStatuses(byte reservationStatus, byte orderStatus, byte paymentStatus)
            => JsonSerializer.Serialize(new
            {
                ReservationStatus = reservationStatus,
                OrderStatus = orderStatus,
                PaymentStatus = paymentStatus
            });
    }
}