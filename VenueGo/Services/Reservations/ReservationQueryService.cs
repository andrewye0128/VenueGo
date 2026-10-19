using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Dtos.ReservationDtos;
using VenueGo.Enums;
using VenueGo.Services.VenueSchedules;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約查詢服務的實作。
    /// <para>
    /// 【為何拆成多個小查詢而不寫一個大 join】
    /// 詳細頁的資料橫跨八張表，而其中訂單明細與操作紀錄都是「一對多」。
    /// 若全部寫在一個 join 裡，主檔會被明細的列數乘開，
    /// 得再用 GroupBy 收回來，程式會變得很難讀，也容易算錯。
    /// 拆成幾個各自負責一段的查詢，每個方法都短到能一眼讀完。
    /// </para>
    /// <para>
    /// 【資料庫未建立外鍵】Entity 沒有導覽屬性，所有關聯都以手動 join
    /// 或條件查詢完成。
    /// </para>
    /// </summary>
    public class ReservationQueryService : IReservationQueryService
    {
        private readonly dbVenueContext _db;
        private readonly IVenueScheduleService _venueSchedule;

        /// <summary>
        /// 尖峰時間由場地模組（IVenueScheduleService）負責，這裡不直接查它的資料表，
        /// 場地模組改了規則也不用跟著改。
        /// </summary>
        public ReservationQueryService(dbVenueContext db, IVenueScheduleService venueSchedule)
        {
            _db = db;
            _venueSchedule = venueSchedule;
        }

        // 取得預約詳細頁所需的完整資料。查無此預約時回傳 null。
        public async Task<ReservationDetailViewModel?> GetDetailAsync(
            int reservationId, CancellationToken cancellationToken = default)
        {
            // 主檔查不到就直接結束，後面的查詢都不必做
            var core = await LoadCoreAsync(reservationId, cancellationToken);
            if (core is null) return null;

            var order = await LoadOrderAsync(reservationId, cancellationToken);
            var payment = order is null
                ? null
                : await LoadPaymentAsync(order.OrderId, cancellationToken);

            var lines = order is null
                ? new List<ReservationDetailLineViewModel>()
                : await LoadLinesAsync(order.OrderId, core.SportTypeId, core.BookingDate, cancellationToken);

            var createdBy = await LoadOperatorAsync(core.CreatedBy, cancellationToken);
            var cancelledBy = await LoadOperatorAsync(core.CancelledBy, cancellationToken);
            var auditLogs = await LoadAuditLogsAsync(reservationId, cancellationToken);

            return Compose(core, order, payment, lines, createdBy, cancelledBy, auditLogs);
        }

        // 取得某位會員的預約列表（篩選、排序、分頁）。
        // ── 前台：我的預約列表 ──────────────────────────────

        /// <summary>每頁筆數上限，避免一次撈出全部資料。</summary>
        private const int MaxPageSize = 50;

        public async Task<PagedResult<MyReservationDto>> GetMyReservationsAsync(
            int userId,
            MyReservationQuery query,
            CancellationToken cancellationToken = default)
        {
            // 1. 頁碼、每頁筆數超出範圍時自動修正，不回錯誤
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            // 2. 這位會員的預約（此時只是組好查詢，還沒送到資料庫）
            var reservations = BuildMyReservationsQuery(userId);

            // 3. 套用篩選條件
            reservations = ApplyMyReservationFilters(reservations, query);

            // 4. 總筆數：篩選後、分頁前（第 1 次送到資料庫）
            var totalCount = await reservations.CountAsync(cancellationToken);

            // 5. 排序 → 跳過前面幾頁 → 取這一頁（第 2 次送到資料庫）
            //    建立時間新到舊：剛訂的預約排最上面
            //    最後加 ReservationId，讓順序固定（建立時間可能相同），換頁時不會重複或漏掉
            var items = await reservations
                .OrderByDescending(r => r.ReservedAt)
                .ThenByDescending(r => r.ReservationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<MyReservationDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }


        /// <summary>
        /// 這位會員的預約（不含已作廢），組成 MyReservationDto。
        /// <para>
        /// 訂單、付款用 left join：沒有訂單或付款紀錄的預約也要留下，
        /// 這時人數、金額、付款欄位是 null。
        /// </para>
        /// <para>
        /// 付款取最新一筆（CreatedAt 最晚）：最新的才代表目前狀態。
        /// </para>
        /// </summary>
        private IQueryable<MyReservationDto> BuildMyReservationsQuery(int userId)
        {
            return from r in _db.Reservations.AsNoTracking()
                   join v in _db.Venues on r.VenueId equals v.VenueId
                   join st in _db.SportTypes on v.SportTypeId equals st.SportTypeId
                   join o in _db.Orders on r.ReservationId equals o.ReservationId into orders
                   from o in orders.DefaultIfEmpty()
                   let payment = _db.Payments
                       .Where(p => o != null && p.OrderId == o.OrderId)
                       .OrderByDescending(p => p.CreatedAt)
                       .ThenByDescending(p => p.PaymentId)
                       .FirstOrDefault()
                   where r.UserId == userId
                      && r.ReservationStatus != (byte)ReservationStatus.Voided
                   select new MyReservationDto
                   {
                       ReservationId = r.ReservationId,
                       BookingDate = r.BookingDate,
                       StartTime = r.StartTime,
                       EndTime = r.EndTime,
                       ReservationStatus = r.ReservationStatus,
                       ReservedAt = r.ReservedAt,
                       CancelledAt = r.CancelledAt,
                       CancelReason = r.CancelReason,

                       VenueName = v.VenueName,
                       SportName = st.SportName,

                       // 沒有訂單時 o 是 null
                       OrderNo = o == null ? null : o.OrderNo,
                       PersonCount = o == null ? null : (int?)o.PersonMount,
                       TotalAmount = o == null ? null : (int?)o.TotalAmount,

                       // 沒有付款紀錄時 payment 是 null
                       PaymentStatus = payment == null ? null : (byte?)payment.PaymentStatus,
                       PaymentDueAt = payment == null ? null : (DateTime?)payment.PaymentDueAt
                   };
        }

        /// <summary>
        /// 套用篩選條件；條件是 null（前端選「全部」）就不篩選。
        /// </summary>
        private static IQueryable<MyReservationDto> ApplyMyReservationFilters(
            IQueryable<MyReservationDto> reservations, MyReservationQuery query)
        {
            // 關鍵字：場地名稱或訂單編號「包含」
            // 前台不顯示預約編號（R012），所以不比對預約編號：
            // 不然客人輸入 12，會找到一筆畫面上看不到 12 的預約，看不懂為什麼
            var keyword = query.Keyword?.Trim();
            if (!string.IsNullOrEmpty(keyword))
            {
                reservations = reservations.Where(r =>
                    r.VenueName.Contains(keyword)
                    || (r.OrderNo != null && r.OrderNo.Contains(keyword)));
            }

            if (query.Date is DateOnly date)
            {
                reservations = reservations.Where(r => r.BookingDate == date);
            }

            if (query.ReservationStatus is byte reservationStatus)
            {
                reservations = reservations.Where(r => r.ReservationStatus == reservationStatus);
            }

            if (query.PaymentStatus is byte paymentStatus)
            {
                reservations = reservations.Where(r => r.PaymentStatus == paymentStatus);
            }

            return reservations;
        }







        // ── 各段查詢 ────────────────────────────────────

        /// <summary>預約主檔 + 會員 + 場地 + 運動類型。這四張表都是一對一，可安全 join。</summary>
        private async Task<CoreRow?> LoadCoreAsync(int reservationId, CancellationToken ct)
        {
            return await (from r in _db.Reservations.AsNoTracking()
                          join u in _db.Users on r.UserId equals u.UserId
                          join v in _db.Venues on r.VenueId equals v.VenueId
                          join st in _db.SportTypes on v.SportTypeId equals st.SportTypeId
                          where r.ReservationId == reservationId
                          select new CoreRow
                          {
                              ReservationId = r.ReservationId,
                              ReservationStatus = r.ReservationStatus,
                              BookingDate = r.BookingDate,
                              StartTime = r.StartTime,
                              EndTime = r.EndTime,
                              ReservedAt = r.ReservedAt,
                              Source = r.Source,
                              TermsVersion = r.TermsVersion,
                              TermsAcceptedAt = r.TermsAcceptedAt,
                              CreatedBy = r.CreatedBy,
                              CancelledBy = r.CancelledBy,
                              CancelledAt = r.CancelledAt,
                              CancelReason = r.CancelReason,

                              UserId = u.UserId,
                              MemberName = u.Name,
                              MemberPhone = u.Phone,
                              MemberEmail = u.Email,
                              MemberCumulativeConsumption = u.CumulativeConsumption,

                              VenueId = v.VenueId,
                              VenueName = v.VenueName,
                              VenueLocation = v.Location,
                              VenueCapacity = v.Capacity,
                              VenuePhotoPath = v.PhotoPath,
                              SportTypeId = v.SportTypeId,
                              SportName = st.SportName
                          })
                         .FirstOrDefaultAsync(ct);
        }

        /// <summary>訂單。UQ_Orders_ReservationId 保證一筆預約最多一張訂單。</summary>
        private async Task<OrderRow?> LoadOrderAsync(int reservationId, CancellationToken ct)
        {
            return await _db.Orders.AsNoTracking()
                .Where(o => o.ReservationId == reservationId)
                .Select(o => new OrderRow
                {
                    OrderId = o.OrderId,
                    OrderNo = o.OrderNo,
                    OrderStatus = o.OrderStatus,
                    OrderCreatedAt = o.OrderCreatedAt,
                    PersonAmount = o.PersonMount,
                    InvoiceType = o.InvoiceType,
                    CarrierNo = o.CarrierNo,
                    TotalAmount = o.TotalAmount
                })
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// 付款紀錄。
        /// 期中一張訂單只有一筆付款；期末若支援補款或分期會有多筆，
        /// 屆時這裡要改成回傳清單並在畫面上列出。
        /// </summary>
        private async Task<PaymentRow?> LoadPaymentAsync(int orderId, CancellationToken ct)
        {
            return await _db.Payments.AsNoTracking()
                .Where(p => p.OrderId == orderId)
                .OrderBy(p => p.PaymentId)
                .Select(p => new PaymentRow
                {
                    PaymentId = p.PaymentId,
                    PaymentStatus = p.PaymentStatus,
                    PaymentMethod = p.PaymentMethod,
                    PaymentChannel = p.PaymentChannel,
                    Amount = p.Amount,
                    PaymentDueAt = p.PaymentDueAt,
                    PaidAt = p.PaidAt,
                    TransactionNo = p.TransactionNo
                })
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// 費用明細，一格時段一列（方案 C）。
        /// <para>
        /// 尖峰標籤由 SlotTime 與目前的計價規則比對得出，僅供顯示；
        /// 金額一律以 OrdersDetails.UnitPrice 的成交快照為準。
        /// </para>
        /// </summary>
        private async Task<List<ReservationDetailLineViewModel>> LoadLinesAsync(
            int orderId, int sportTypeId, DateOnly bookingDate, CancellationToken ct)
        {
            // 預約那一天的尖峰起始時間（每個星期幾可能不同），由場地模組提供
            var peakStartTime = await _venueSchedule.GetPeakStartTimeAsync(sportTypeId, bookingDate, ct);

            var rows = await _db.OrdersDetails.AsNoTracking()
                .Where(d => d.OrderId == orderId)
                .OrderBy(d => d.SlotTime)
                .Select(d => new
                {
                    d.SlotTime,
                    d.UnitPrice,
                    d.DurationHours,
                    d.Subtotal
                })
                .ToListAsync(ct);

            return rows
                .Select(d => new ReservationDetailLineViewModel
                {
                    SlotTime = d.SlotTime,
                    UnitPrice = d.UnitPrice,
                    DurationHours = d.DurationHours,
                    Subtotal = d.Subtotal,
                    IsPeak = IsPeakSlot(d.SlotTime, peakStartTime)
                })
                .ToList();
        }
        /// <summary>操作人員的姓名與員工代碼。userId 為 null 時回傳 null。</summary>
        private async Task<OperatorRow?> LoadOperatorAsync(int? userId, CancellationToken ct)
        {
            if (userId is null) return null;

            return await (from u in _db.Users.AsNoTracking()
                          join e in _db.Employees on u.UserId equals e.UserId into employees
                          from e in employees.DefaultIfEmpty()
                          where u.UserId == userId.Value
                          select new OperatorRow
                          {
                              UserId = u.UserId,
                              Name = u.Name,
                              // 會員沒有員工資料，left join 後 e 會是 null
                              EmployeeNo = e == null ? null : e.EmployeeNo
                          })
                         .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// 這筆預約的操作紀錄，依時間新到舊。
        /// AuditLogs.EntityID 是 nvarchar，因此要用字串比對。
        /// </summary>
        private async Task<List<ReservationAuditLogViewModel>> LoadAuditLogsAsync(
            int reservationId, CancellationToken ct)
        {
            var entityId = reservationId.ToString();

            return await (from log in _db.AuditLogs.AsNoTracking()
                          join u in _db.Users on log.UserId equals u.UserId into users
                          from u in users.DefaultIfEmpty()
                          where log.EntityType == AuditEntityTypes.Reservations
                                && log.EntityId == entityId
                          orderby log.CreatedAt descending
                          select new ReservationAuditLogViewModel
                          {
                              CreatedAt = log.CreatedAt,
                              Action = log.Action,
                              // 系統自動執行的紀錄沒有操作者
                              OperatorName = u == null ? null : u.Name
                          })
                         .ToListAsync(ct);
        }

        // ── 組裝 ───────────────────────────────────────

        /// <summary>把各段查詢的結果組成一個 ViewModel。</summary>
        private static ReservationDetailViewModel Compose(
            CoreRow core,
            OrderRow? order,
            PaymentRow? payment,
            IReadOnlyList<ReservationDetailLineViewModel> lines,
            OperatorRow? createdBy,
            OperatorRow? cancelledBy,
            IReadOnlyList<ReservationAuditLogViewModel> auditLogs)
        {
            return new ReservationDetailViewModel
            {
                ReservationId = core.ReservationId,
                ReservationStatus = (ReservationStatus)core.ReservationStatus,
                BookingDate = core.BookingDate,
                StartTime = core.StartTime,
                EndTime = core.EndTime,
                ReservedAt = core.ReservedAt,
                Source = (ReservationSource)core.Source,
                TermsVersion = core.TermsVersion,
                TermsAcceptedAt = core.TermsAcceptedAt,

                UserId = core.UserId,
                MemberName = core.MemberName,
                MemberPhone = core.MemberPhone,
                MemberEmail = core.MemberEmail,
                MemberCumulativeConsumption = core.MemberCumulativeConsumption,

                VenueId = core.VenueId,
                VenueName = core.VenueName,
                SportName = core.SportName,
                VenueLocation = core.VenueLocation,
                VenueCapacity = core.VenueCapacity,
                VenuePhotoPath = core.VenuePhotoPath,

                OrderId = order?.OrderId,
                OrderNo = order?.OrderNo ?? string.Empty,
                OrderStatus = (OrderStatus)(order?.OrderStatus ?? 0),
                OrderCreatedAt = order?.OrderCreatedAt,
                PersonAmount = order?.PersonAmount ?? 0,
                InvoiceType = (InvoiceType)(order?.InvoiceType ?? 0),
                CarrierNo = order?.CarrierNo,
                TotalAmount = order?.TotalAmount ?? 0,
                Lines = lines,

                PaymentId = payment?.PaymentId,
                PaymentStatus = (PaymentStatus)(payment?.PaymentStatus ?? 0),
                PaymentMethod = (PaymentMethod)(payment?.PaymentMethod ?? 0),
                PaymentChannel = (PaymentChannel)(payment?.PaymentChannel ?? 0),
                PaymentAmount = payment?.Amount ?? 0,
                PaymentDueAt = payment?.PaymentDueAt,
                PaidAt = payment?.PaidAt,
                TransactionNo = payment?.TransactionNo,

                CreatedByName = createdBy?.Name,
                CreatedByEmployeeNo = createdBy?.EmployeeNo,
                AuditLogs = auditLogs,

                CancelledByName = cancelledBy?.Name,
                CancelledAt = core.CancelledAt,
                CancelReason = core.CancelReason
            };
        }

        /// <summary>
        /// 判斷時段是否落在尖峰區間。
        /// 尖峰起始時間依預約日期是星期幾而定，由場地模組（SportTypePeakHours）提供；
        /// 規則是「起始時間 &gt;= 尖峰起始時間即為尖峰」，一路延續到打烊。
        /// 此判斷與 TimeSlotService 一致。
        /// </summary>
        private static bool IsPeakSlot(TimeOnly? slotTime, TimeOnly? peakStartTime)
        {
            if (slotTime is null || peakStartTime is null) return false;

            return slotTime.Value >= peakStartTime.Value;
        }

        // ── 各段查詢的中繼型別 ───────────────────────────
        // 之所以不直接投影成 ViewModel，是因為組裝時需要處理
        // 訂單或付款不存在（null）的情況，以及列舉型別的轉換。

        private sealed class CoreRow
        {
            public int ReservationId { get; init; }
            public byte ReservationStatus { get; init; }
            public DateOnly BookingDate { get; init; }
            public TimeOnly StartTime { get; init; }
            public TimeOnly EndTime { get; init; }
            public DateTime ReservedAt { get; init; }
            public byte Source { get; init; }
            public string TermsVersion { get; init; } = string.Empty;
            public DateTime TermsAcceptedAt { get; init; }
            public int? CreatedBy { get; init; }
            public int? CancelledBy { get; init; }
            public DateTime? CancelledAt { get; init; }
            public string? CancelReason { get; init; }

            public int UserId { get; init; }
            public string MemberName { get; init; } = string.Empty;
            public string MemberPhone { get; init; } = string.Empty;
            public string MemberEmail { get; init; } = string.Empty;
            public int MemberCumulativeConsumption { get; init; }

            public int VenueId { get; init; }
            public string VenueName { get; init; } = string.Empty;
            public string VenueLocation { get; init; } = string.Empty;
            public int? VenueCapacity { get; init; }
            public string? VenuePhotoPath { get; init; }
            public int SportTypeId { get; init; }
            public string SportName { get; init; } = string.Empty;
        }

        private sealed class OrderRow
        {
            public int OrderId { get; init; }
            public string OrderNo { get; init; } = string.Empty;
            public byte OrderStatus { get; init; }
            public DateTime OrderCreatedAt { get; init; }
            public int PersonAmount { get; init; }
            public byte InvoiceType { get; init; }
            public string? CarrierNo { get; init; }
            public int TotalAmount { get; init; }
        }

        private sealed class PaymentRow
        {
            public int PaymentId { get; init; }
            public byte PaymentStatus { get; init; }
            public byte PaymentMethod { get; init; }
            public byte PaymentChannel { get; init; }
            public int Amount { get; init; }
            public DateTime PaymentDueAt { get; init; }
            public DateTime? PaidAt { get; init; }
            public string? TransactionNo { get; init; }
        }

        private sealed class OperatorRow
        {
            public int UserId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string? EmployeeNo { get; init; }
        }
    }
}