using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Models.Enums;
using VenueGo.Models.Options;
using VenueGo.Models.TimeSlots;

namespace VenueGo.Services.TimeSlots
{
    /// <summary>
    /// 時段服務的實作。
    /// <para>
    /// 【計算流程】不論查一天或一整個月，都是同樣三個步驟：
    /// </para>
    /// <list type="number">
    ///   <item>由 WeekBusinessHours 的營業時間展開成一小時一格的清單（分母）</item>
    ///   <item>由 VenueUnavailableSlots 標記維護中的格子</item>
    ///   <item>由 ReservationSlots 標記已被他人預約的格子</item>
    /// </list>
    /// <para>
    /// 【為何 ReservationSlots 不必過濾狀態】依專案的設計，
    /// 預約取消時會刪除該筆的 ReservationSlots 資料列，
    /// 因此這張表中存在的每一列都代表「目前有效的佔用」。
    /// 歷史資料仍保留在 Reservations 主檔（含 VenueId、BookingDate、StartTime、EndTime），
    /// 不會因為刪除佔位而遺失。
    /// </para>
    /// </summary>
    public class TimeSlotService : ITimeSlotService
    {
        private readonly dbVenueContext _db;
        private readonly ReservationRulesOptions _rules;

        public TimeSlotService(dbVenueContext db, IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _db = db;
            _rules = rules.Value;
        }

        public async Task<IReadOnlyList<TimeSlotStatus>> GetDaySlotsAsync(
            int venueId, DateOnly date, CancellationToken cancellationToken = default)
        {
            // 步驟一：當天的營業時段（分母）
            var businessHours = await GetBusinessHoursAsync(date, cancellationToken);
            if (businessHours is null) return Array.Empty<TimeSlotStatus>();

            var slotTimes = ExpandToSlots(businessHours.Value.Open, businessHours.Value.Close);
            if (slotTimes.Count == 0) return Array.Empty<TimeSlotStatus>();

            // 步驟二：維護中的時段
            var unavailable = await _db.VenueUnavailableSlots.AsNoTracking()
                .Where(u => u.VenueId == venueId && u.UnavailableDate == date)
                .Select(u => u.UnavailableTime)
                .ToListAsync(cancellationToken);

            // 步驟三：已被預約的時段
            var booked = await _db.ReservationSlots.AsNoTracking()
                .Where(s => s.VenueId == venueId && s.BookingDate == date)
                .Select(s => s.SlotTime)
                .ToListAsync(cancellationToken);

            var unavailableSet = unavailable.ToHashSet();
            var bookedSet = booked.ToHashSet();

            // 計價規則。整天共用同一組規則，所以只查一次。
            var priceRule = await GetPriceRuleAsync(venueId, cancellationToken);

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            return slotTimes
                .Select(slotTime => BuildSlotStatus(
                    slotTime, date, today, currentTime, unavailableSet, bookedSet, priceRule))
                .ToList();
        }

        public async Task<IReadOnlyDictionary<DateOnly, DayAvailability>> GetRangeAvailabilityAsync(
            int venueId, DateOnly fromDate, DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            if (toDate < fromDate)
            {
                return new Dictionary<DateOnly, DayAvailability>();
            }

            // 一次把整個範圍的營業時間、維護時段、已預約時段全部取出，
            // 再於記憶體中逐日比對。
            // 範圍最多一個月，資料量約 31 天 × 12 格 = 372 列，
            // 一次取出遠比每天各查一次（93 次往返）便宜。
            var weeklyHours = await GetWeeklyBusinessHoursAsync(cancellationToken);

            var unavailable = await _db.VenueUnavailableSlots.AsNoTracking()
                .Where(u => u.VenueId == venueId
                            && u.UnavailableDate >= fromDate
                            && u.UnavailableDate <= toDate)
                .Select(u => new { u.UnavailableDate, u.UnavailableTime })
                .ToListAsync(cancellationToken);

            var booked = await _db.ReservationSlots.AsNoTracking()
                .Where(s => s.VenueId == venueId
                            && s.BookingDate >= fromDate
                            && s.BookingDate <= toDate)
                .Select(s => new { s.BookingDate, s.SlotTime })
                .ToListAsync(cancellationToken);

            var unavailableByDate = unavailable
                .GroupBy(x => x.UnavailableDate)
                .ToDictionary(g => g.Key, g => g.Select(x => x.UnavailableTime).ToHashSet());

            var bookedByDate = booked
                .GroupBy(x => x.BookingDate)
                .ToDictionary(g => g.Key, g => g.Select(x => x.SlotTime).ToHashSet());

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            var result = new Dictionary<DateOnly, DayAvailability>();

            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                if (!weeklyHours.TryGetValue((byte)date.DayOfWeek, out var hours))
                {
                    // 該星期未設定營業時間，視為公休
                    result[date] = new DayAvailability { Date = date, TotalSlots = 0, AvailableSlots = 0 };
                    continue;
                }

                var slotTimes = ExpandToSlots(hours.Open, hours.Close);

                unavailableByDate.TryGetValue(date, out var dayUnavailable);
                bookedByDate.TryGetValue(date, out var dayBooked);

                var availableCount = slotTimes.Count(slotTime => IsAvailable(
                    slotTime, date, today, currentTime, dayUnavailable, dayBooked));

                result[date] = new DayAvailability
                {
                    Date = date,
                    TotalSlots = slotTimes.Count,
                    AvailableSlots = availableCount
                };
            }

            return result;
        }

        // ── 以下為共用的私有邏輯 ────────────────────────────

        /// <summary>
        /// 把營業時間展開成一小時一格的起始時間清單。
        /// <para>
        /// 終止條件是 slotTime &lt; close 而非 &lt;=。
        /// 因為每一格代表「從這個時間開始的一小時」：
        /// 營業到 22:00 時，最後一格是 21:00-22:00。
        /// 若寫成 &lt;= 就會多產生一格 22:00-23:00，超出營業時間。
        /// 這是整個服務唯一容易寫錯的地方。
        /// </para>
        /// </summary>
        private static List<TimeOnly> ExpandToSlots(TimeOnly? open, TimeOnly? close)
        {
            if (open is null || close is null || close <= open)
            {
                return new List<TimeOnly>();
            }

            var slots = new List<TimeOnly>();
            for (var slotTime = open.Value; slotTime < close.Value; slotTime = slotTime.AddHours(1))
            {
                slots.Add(slotTime);
            }
            return slots;
        }

        /// <summary>
        /// 判斷單一時段是否可預約。
        /// 供日曆的統計使用，與 BuildSlotStatus 共用同一套判斷順序。
        /// </summary>
        private bool IsAvailable(
            TimeOnly slotTime, DateOnly date, DateOnly today, TimeOnly currentTime,
            HashSet<TimeOnly>? unavailable, HashSet<TimeOnly>? booked)
            => DetermineAvailability(slotTime, date, today, currentTime, unavailable, booked)
               == SlotAvailability.Available;

        /// <summary>
        /// 判斷單一時段的狀態。
        /// <para>
        /// 【判斷順序有意義】先判斷是否已過時間，再判斷維護，最後才判斷已預約。
        /// 因為一個時段可能同時符合多個條件（例如已被預約、之後又被設為維護），
        /// 顯示時只能挑一個原因，而「已過時間」和「維護中」對櫃檯的意義
        /// 比「已預約」更重要——前兩者代表這個場地當下根本不能用。
        /// </para>
        /// </summary>
        private SlotAvailability DetermineAvailability(
            TimeOnly slotTime, DateOnly date, DateOnly today, TimeOnly currentTime,
            HashSet<TimeOnly>? unavailable, HashSet<TimeOnly>? booked)
        {
            if (date < today)
            {
                return SlotAvailability.Past;
            }

            if (date == today)
            {
                // 今天可以預約，但不能預約已經開始的時段。
                // 現在 19:10 時，19:00-20:00 那格已經開始，最早只能選 20:00。
                // 若營運上希望允許臨時入場收當前這一小時，
                // 把 appsettings.json 的 AllowCurrentHourSlot 設為 true。
                var hasStarted = _rules.AllowCurrentHourSlot
                    ? slotTime.AddHours(1) <= currentTime
                    : slotTime <= currentTime;

                if (hasStarted) return SlotAvailability.Past;
            }

            if (unavailable is not null && unavailable.Contains(slotTime))
            {
                return SlotAvailability.Unavailable;
            }

            if (booked is not null && booked.Contains(slotTime))
            {
                return SlotAvailability.Booked;
            }

            return SlotAvailability.Available;
        }

        /// <summary>組出單一時段的完整狀態，含單價。</summary>
        private TimeSlotStatus BuildSlotStatus(
            TimeOnly slotTime, DateOnly date, DateOnly today, TimeOnly currentTime,
            HashSet<TimeOnly> unavailable, HashSet<TimeOnly> booked,
            PriceRule? priceRule)
        {
            var isPeak = priceRule?.PeakStartTime is not null
                         && slotTime >= priceRule.Value.PeakStartTime.Value;

            var unitPrice = priceRule is null
                ? (int?)null
                : isPeak ? priceRule.Value.PeakPrice : priceRule.Value.OffPeakPrice;

            return new TimeSlotStatus
            {
                SlotTime = slotTime,
                Availability = DetermineAvailability(
                    slotTime, date, today, currentTime, unavailable, booked),
                UnitPrice = unitPrice,
                IsPeak = isPeak
            };
        }

        /// <summary>取得某一天的營業時間。查無設定或當天公休時回傳 null。</summary>
        private async Task<(TimeOnly? Open, TimeOnly? Close)?> GetBusinessHoursAsync(
            DateOnly date, CancellationToken cancellationToken)
        {
            // DayOfWeek 的編碼與 C# 的 System.DayOfWeek 一致：0 = 星期日、6 = 星期六。
            // 這個約定必須與資料庫中的資料一致，否則整個時段計算會偏移一天。
            var dayOfWeek = (byte)date.DayOfWeek;

            var hours = await _db.WeekBusinessHours.AsNoTracking()
                .Where(h => h.DayOfWeek == dayOfWeek && h.IsOpen)
                .Select(h => new { h.OpenTime, h.CloseTime })
                .FirstOrDefaultAsync(cancellationToken);

            return hours is null ? null : (hours.OpenTime, hours.CloseTime);
        }

        /// <summary>一次取出七天的營業時間，供範圍查詢使用。</summary>
        private async Task<Dictionary<byte, (TimeOnly? Open, TimeOnly? Close)>>
            GetWeeklyBusinessHoursAsync(CancellationToken cancellationToken)
        {
            var rows = await _db.WeekBusinessHours.AsNoTracking()
                .Where(h => h.IsOpen)
                .Select(h => new { h.DayOfWeek, h.OpenTime, h.CloseTime })
                .ToListAsync(cancellationToken);

            return rows.ToDictionary(
                h => h.DayOfWeek,
                h => ((TimeOnly?)h.OpenTime, (TimeOnly?)h.CloseTime));
        }

        /// <summary>
        /// 取得場地所屬運動類型的計價規則。
        /// <para>
        /// SportTypePriceRules 對每種運動只有一列（UQ_SportTypePriceRules_SportTypeId），
        /// 因此沒有價格歷史。這也是為什麼 OrdersDetails 必須存下當時的單價快照——
        /// 老闆調價之後，舊訂單的金額不能跟著變動。
        /// </para>
        /// </summary>
        private async Task<PriceRule?> GetPriceRuleAsync(
            int venueId, CancellationToken cancellationToken)
        {
            var rule = await (from v in _db.Venues.AsNoTracking()
                              join r in _db.SportTypePriceRules
                                  on v.SportTypeId equals r.SportTypeId
                              where v.VenueId == venueId
                              select new
                              {
                                  r.PeakStartTime,
                                  r.PeakPrice,
                                  r.OffPeakPrice
                              })
                             .FirstOrDefaultAsync(cancellationToken);

            if (rule is null) return null;

            return new PriceRule
            {
                PeakStartTime = rule.PeakStartTime,
                PeakPrice = rule.PeakPrice,
                OffPeakPrice = rule.OffPeakPrice
            };
        }

        /// <summary>
        /// 計價規則的輕量結構。
        /// <para>
        /// 注意 SportTypePriceRules 只有 PeakStartTime 沒有 PeakEndTime，
        /// 因此判斷規則只能是「時段起始時間 &gt;= PeakStartTime 即為尖峰」，
        /// 也就是尖峰一路延續到打烊。若日後新增 PeakEndTime 欄位，
        /// 判斷邏輯集中在 BuildSlotStatus 一處，只需改那一行。
        /// </para>
        /// </summary>
        private readonly struct PriceRule
        {
            public TimeOnly? PeakStartTime { get; init; }
            public int PeakPrice { get; init; }
            public int OffPeakPrice { get; init; }
        }
    }
}