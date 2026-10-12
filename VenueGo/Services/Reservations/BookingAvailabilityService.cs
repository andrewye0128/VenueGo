using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Dtos.BookingDtos;
using VenueGo.Models.Entities;
using VenueGo.Models.Options;
using VenueGo.Services.TimeSlots;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 前台場地預約頁的查詢服務實作。說明見 <see cref="IBookingAvailabilityService"/>。
    /// </summary>
    public class BookingAvailabilityService : IBookingAvailabilityService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeSlotService _timeSlots;
        private readonly ITimeService _timeService;
        private readonly ReservationRulesOptions _rules;

        /// <summary>
        /// 時段篩選的時間範圍（開始含、結束不含）。
        /// key 要跟前端 src/constants/booking.js 的 TIME_PERIODS 一致。
        /// 晚上是「18:00 之後」：TimeOnly 沒有 24:00，所以結束時間用 null 表示不限。
        /// </summary>
        private static readonly Dictionary<string, (TimeOnly? From, TimeOnly? To)> Periods = new()
        {
            ["morning"] = (null, new TimeOnly(12, 0)),
            ["afternoon"] = (new TimeOnly(12, 0), new TimeOnly(18, 0)),
            ["evening"] = (new TimeOnly(18, 0), null),
        };

        /// <param name="db">資料庫。</param>
        /// <param name="timeSlots">時段服務（使用一次查多個場地的批次方法）。</param>
        /// <param name="timeService">網站時間，用來判斷今天。</param>
        /// <param name="rules">預約規則（會員可預約的日期範圍）。</param>
        public BookingAvailabilityService(
            dbVenueContext db,
            ITimeSlotService timeSlots,
            ITimeService timeService,
            IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _db = db;
            _timeSlots = timeSlots;
            _timeService = timeService;
            _rules = rules.Value;
        }

        public bool IsWithinBookingRange(DateOnly date)
           => _rules.IsWithinMemberRange(date, DateOnly.FromDateTime(_timeService.Today));

        public async Task<BookingAvailabilityDto> GetDayAvailabilityAsync(
            DateOnly date, int? sportTypeId, CancellationToken cancellationToken = default)
        {
            // 第 1 次查詢：可預約的場地
            var venues = await GetBookableVenuesAsync(sportTypeId, cancellationToken);

            // 第 2～5 次查詢：這些場地這天的時段（一次查完，不是一個場地查一次）
            var slotsByVenue = await _timeSlots.GetDaySlotsForVenuesAsync(
                venues.Select(v => v.VenueId).ToList(), date, cancellationToken);

            return new BookingAvailabilityDto
            {
                Date = date,
                Venues = venues.Select(v => new VenueAvailabilityDto
                {
                    VenueId = v.VenueId,
                    SportTypeId = v.SportTypeId,
                    VenueName = v.VenueName,
                    Capacity = v.Capacity,
                    PhotoPath = v.PhotoPath,
                    Slots = slotsByVenue[v.VenueId]
                        .Select(s => new SlotDto
                        {
                            // 用 FormValue（"18:00"），不要用 SlotTime（TimeOnly 會變成 "18:00:00"）
                            SlotTime = s.FormValue,
                            Availability = (byte)s.Availability,
                            UnitPrice = s.UnitPrice,
                            IsPeak = s.IsPeak,
                        })
                        .ToList(),
                }).ToList(),
            };
        }


        public async Task<IReadOnlyList<DayAvailabilitySummaryDto>> GetDaySummariesAsync(
            DateOnly fromDate, DateOnly toDate, int? sportTypeId, string? period,
            CancellationToken cancellationToken = default)
        {
            // 時段篩選 → 時間範圍；沒傳或不認得的值就算整天
            var (fromTime, toTime) = period is not null && Periods.TryGetValue(period, out var range)
                ? range
                : (null, null);

            // 第 1 次查詢：可預約的場地
            var venues = await GetBookableVenuesAsync(sportTypeId, cancellationToken);

            // 第 2～4 次查詢：這些場地這段日期每天的統計
            var byVenue = await _timeSlots.GetRangeAvailabilityForVenuesAsync(
                venues.Select(v => v.VenueId).ToList(),
                fromDate, toDate, fromTime, toTime, cancellationToken);

            // 每一天：所有場地的「剩幾格」加總。沒有場地時每天都是 0，日期列才不會缺天
            var result = new List<DayAvailabilitySummaryDto>();
            for (var date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                var count = byVenue.Values.Sum(
                    days => days.TryGetValue(date, out var day) ? day.AvailableSlots : 0);
                result.Add(new DayAvailabilitySummaryDto { Date = date, AvailableCount = count });
            }

            return result;
        }


        /// <summary>
        /// 可預約的場地：場地啟用，而且它的運動也啟用。依運動、場地名稱排序。
        /// </summary>
        private Task<List<Venue>> GetBookableVenuesAsync(int? sportTypeId, CancellationToken cancellationToken)
            => (from v in _db.Venues.AsNoTracking()
                join s in _db.SportTypes on v.SportTypeId equals s.SportTypeId
                where v.IsActive && s.IsActive
                      && (sportTypeId == null || v.SportTypeId == sportTypeId)
                orderby v.SportTypeId, v.VenueName
                select v)
               .ToListAsync(cancellationToken);
    }
}
