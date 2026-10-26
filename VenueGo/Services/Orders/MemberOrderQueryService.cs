using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Dtos.OrderDtos;
using VenueGo.Enums;
using VenueGo.Models.ReservationModels;
using VenueGo.Options;
using VenueGo.Services.VenueSchedules;

namespace VenueGo.Services.Orders
{
    /// <summary>
    /// 前台會員「訂單與付款」查詢的實作。
    /// <para>
    /// 列表和明細共用 BuildMyOrdersQuery：「哪些訂單算是這位會員的」只寫一次，
    /// 明細就不會不小心讓人看到列表看不到的訂單。
    /// </para>
    /// </summary>
    public class MemberOrderQueryService : IMemberOrderQueryService
    {
        /// <summary>每頁筆數上限，避免一次撈出全部資料。</summary>
        private const int MaxPageSize = 50;

        private readonly dbVenueContext _db;
        private readonly IVenueScheduleService _venueSchedule;
        private readonly ReservationRulesOptions _rules;

        public MemberOrderQueryService(
            dbVenueContext db,
            IVenueScheduleService venueSchedule,
            IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _db = db;
            _venueSchedule = venueSchedule;
            // IOptionsSnapshot：每個請求重新讀一次設定，改 appsettings 不用重新啟動
            _rules = rules.Value;
        }

        // ── 列表 ───────────────────────────────────────

        public async Task<PagedResult<MyOrderDto>> GetMyOrdersAsync(
            int userId, MyOrderQuery query, CancellationToken cancellationToken = default)
        {
            // 1. 頁碼、每頁筆數超出範圍時自動修正，不回錯誤
            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

            // 2. 這位會員的訂單＋篩選條件（此時只是組好查詢，還沒送到資料庫）
            var orders = ApplyMyOrderFilters(BuildMyOrdersQuery(userId), query);

            // 3. 總筆數：篩選後、分頁前（第 1 次送到資料庫）
            var totalCount = await orders.CountAsync(cancellationToken);

            // 4. 排序 → 分頁（第 2 次送到資料庫）
            //    最後加 OrderId，讓順序固定（下單時間可能相同），換頁時不會重複或漏掉
            var items = await orders
                .OrderByDescending(o => o.OrderCreatedAt)
                .ThenByDescending(o => o.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            // 5. 最晚可以開始付款的時間：要讀設定值，在 C# 這裡算
            foreach (var item in items)
            {
                item.PaymentStartDeadline = GetPaymentStartDeadline(item);
            }

            return new PagedResult<MyOrderDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // ── 明細 ───────────────────────────────────────

        public async Task<MyOrderDetailDto?> GetMyOrderAsync(
            int userId, int orderId, CancellationToken cancellationToken = default)
        {
            // 用列表同一個查詢再加上 OrderId：不是這位會員的、作廢的，這裡就查不到
            var order = await BuildMyOrdersQuery(userId)
                .Where(o => o.OrderId == orderId)
                .FirstOrDefaultAsync(cancellationToken);
            if (order is null) return null;

            // 明細才需要的訂單欄位，以及判斷尖峰要用的運動類型
            var extra = await (from o in _db.Orders.AsNoTracking()
                               join r in _db.Reservations on o.ReservationId equals (int?)r.ReservationId
                               join v in _db.Venues on r.VenueId equals v.VenueId
                               where o.OrderId == orderId
                               select new { o.PersonMount, o.InvoiceType, o.CarrierNo, v.SportTypeId })
                              .FirstAsync(cancellationToken);

            var lines = await LoadLinesAsync(orderId, extra.SportTypeId, order.BookingDate, cancellationToken);
            var payments = await LoadPaymentsAsync(orderId, cancellationToken);

            // 列表的欄位逐一帶過來，再加上明細的欄位
            return new MyOrderDetailDto
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                TotalAmount = order.TotalAmount,
                OrderCreatedAt = order.OrderCreatedAt,
                ReservationId = order.ReservationId,
                BookingDate = order.BookingDate,
                StartTime = order.StartTime,
                EndTime = order.EndTime,
                ReservationStatus = order.ReservationStatus,
                VenueName = order.VenueName,
                SportName = order.SportName,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                PaymentDueAt = order.PaymentDueAt,
                PaidAt = order.PaidAt,
                PaymentStartDeadline = GetPaymentStartDeadline(order),

                PersonCount = extra.PersonMount,
                InvoiceType = extra.InvoiceType,
                CarrierNo = extra.CarrierNo,
                Lines = lines,
                Payments = payments
            };
        }

        // ── 共用查詢 ────────────────────────────────────

        /// <summary>
        /// 這位會員的訂單（有預約、預約沒有作廢），組成 MyOrderDto。
        /// <para>
        /// 用 Reservations.UserId 判斷是誰的：跟「我的預約」同一個條件，兩頁看到的範圍才會一致。
        /// （建立預約時 Orders.UserId 也寫同一位會員。）
        /// </para>
        /// <para>
        /// inner join Reservations：場地、使用時間都來自預約，沒有預約的訂單前台沒東西可以顯示。
        /// 付款取最新一筆（CreatedAt 最晚），寫法同「我的預約」。
        /// </para>
        /// </summary>
        private IQueryable<MyOrderDto> BuildMyOrdersQuery(int userId)
        {
            return from o in _db.Orders.AsNoTracking()
                       // Orders.ReservationId 是 int?，要轉型才能跟 int 比對
                   join r in _db.Reservations on o.ReservationId equals (int?)r.ReservationId
                   join v in _db.Venues on r.VenueId equals v.VenueId
                   join st in _db.SportTypes on v.SportTypeId equals st.SportTypeId
                   let payment = _db.Payments
                       .Where(p => p.OrderId == o.OrderId)
                       .OrderByDescending(p => p.CreatedAt)
                       .ThenByDescending(p => p.PaymentId)
                       .FirstOrDefault()
                   where r.UserId == userId
                      && r.ReservationStatus != (byte)ReservationStatus.Voided
                   select new MyOrderDto
                   {
                       OrderId = o.OrderId,
                       OrderNo = o.OrderNo,
                       TotalAmount = o.TotalAmount,
                       OrderCreatedAt = o.OrderCreatedAt,

                       ReservationId = r.ReservationId,
                       BookingDate = r.BookingDate,
                       StartTime = r.StartTime,
                       EndTime = r.EndTime,
                       ReservationStatus = r.ReservationStatus,

                       VenueName = v.VenueName,
                       SportName = st.SportName,

                       // 沒有付款紀錄時 payment 是 null
                       PaymentMethod = payment == null ? null : (byte?)payment.PaymentMethod,
                       PaymentStatus = payment == null ? null : (byte?)payment.PaymentStatus,
                       PaymentDueAt = payment == null ? null : (DateTime?)payment.PaymentDueAt,
                       PaidAt = payment == null ? null : payment.PaidAt
                   };
        }

        /// <summary>套用篩選條件；條件是 null（前端選「全部」）就不篩選。</summary>
        private static IQueryable<MyOrderDto> ApplyMyOrderFilters(
            IQueryable<MyOrderDto> orders, MyOrderQuery query)
        {
            // 關鍵字：場地名稱或訂單編號「包含」（跟「我的預約」同一條規則）
            var keyword = query.Keyword?.Trim();
            if (!string.IsNullOrEmpty(keyword))
            {
                orders = orders.Where(o =>
                    o.VenueName.Contains(keyword) || o.OrderNo.Contains(keyword));
            }

            if (query.Date is DateOnly date)
            {
                orders = orders.Where(o => o.BookingDate == date);
            }

            if (query.PaymentMethod is byte paymentMethod)
            {
                orders = orders.Where(o => o.PaymentMethod == paymentMethod);
            }

            if (query.PaymentStatus is byte paymentStatus)
            {
                orders = orders.Where(o => o.PaymentStatus == paymentStatus);
            }

            return orders;
        }

        /// <summary>
        /// 最晚可以開始付款的時間。只有「線上付款、未付款或付款失敗、預約待確認」才有值。
        /// <para>前端 utils/order.js 的 canPay 也是這三個條件，再加上「還沒超過這個時間」。</para>
        /// </summary>
        private DateTime? GetPaymentStartDeadline(MyOrderDto order)
        {
            var isPayable =
                order.PaymentMethod == (byte)PaymentMethod.Online
                && (order.PaymentStatus == (byte)PaymentStatus.Unpaid
                    || order.PaymentStatus == (byte)PaymentStatus.Failed)
                && order.ReservationStatus == (byte)ReservationStatus.Pending
                && order.PaymentDueAt.HasValue;

            return isPayable
                ? _rules.GetMemberOnlinePaymentStartDeadline(order.PaymentDueAt!.Value)
                : null;
        }

        /// <summary>
        /// 費用明細，一格一列，依時間排序。
        /// <para>
        /// 尖峰標籤：先用下單時存下來的 OrdersDetails.IsPeak；舊資料沒有才用預約當天的尖峰起始時間推算
        /// （規則在 PeakSlotRules，同後台預約詳細頁）。金額一律用 OrdersDetails.UnitPrice 的成交快照。
        /// </para>
        /// <para>
        /// SlotTime 是 null 的列不回傳：現在的寫入方式每一格都有 SlotTime，
        /// 只有很早以前的資料可能沒有；前端的費用表格要用時間當每一列的 key。
        /// </para>
        /// </summary>
        private async Task<List<MyOrderLineDto>> LoadLinesAsync(
            int orderId, int sportTypeId, DateOnly bookingDate, CancellationToken ct)
        {
            var rows = await _db.OrdersDetails.AsNoTracking()
                .Where(d => d.OrderId == orderId && d.SlotTime != null)
                .OrderBy(d => d.SlotTime)
                .Select(d => new { d.SlotTime, d.UnitPrice, d.IsPeak })
                .ToListAsync(ct);

            // 有舊資料（IsPeak 是 null）才向場地模組查預約當天的尖峰起始時間
            var peakStartTime = PeakSlotRules.NeedsFallback(rows.Select(d => d.IsPeak))
                ? await _venueSchedule.GetPeakStartTimeAsync(sportTypeId, bookingDate, ct)
                : null;

            return rows
                .Select(d => new MyOrderLineDto
                {
                    SlotTime = d.SlotTime!.Value.ToString("HH\\:mm"),
                    UnitPrice = d.UnitPrice,
                    IsPeak = PeakSlotRules.IsPeak(d.IsPeak, d.SlotTime, peakStartTime)
                })
                .ToList();
        }

        /// <summary>全部的付款紀錄，新到舊（列表上的付款狀態就是第一筆）。</summary>
        private async Task<List<MyOrderPaymentDto>> LoadPaymentsAsync(int orderId, CancellationToken ct)
        {
            return await _db.Payments.AsNoTracking()
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.PaymentId)
                .Select(p => new MyOrderPaymentDto
                {
                    PaymentMethod = p.PaymentMethod,
                    PaymentChannel = p.PaymentChannel,
                    PaymentStatus = p.PaymentStatus,
                    Amount = p.Amount,
                    PaymentDueAt = p.PaymentDueAt,
                    PaidAt = p.PaidAt,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(ct);
        }
    }
}