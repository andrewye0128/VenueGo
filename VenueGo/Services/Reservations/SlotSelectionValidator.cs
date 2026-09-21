using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VenueGo.Models.Options;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.TimeSlots;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 時段選取驗證服務的實作。
    /// </summary>
    public class SlotSelectionValidator : ISlotSelectionValidator
    {
        private readonly ITimeSlotService _timeSlotService;
        private readonly ReservationRulesOptions _rules;

        public SlotSelectionValidator(
            ITimeSlotService timeSlotService,
            IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _timeSlotService = timeSlotService;
            _rules = rules.Value;
        }

        public async Task<SlotSelectionResult> ValidateAsync(
            int venueId,
            DateOnly date,
            IReadOnlyList<TimeOnly> slotTimes,
            CancellationToken cancellationToken = default)
        {
            if (slotTimes is null || slotTimes.Count == 0)
            {
                return SlotSelectionResult.Fail("請至少選擇一個時段。");
            }

            // 去除重複。前端不會送出重複值，但手動組表單可以，
            // 若不處理會讓同一格時段被計價兩次。
            var requested = slotTimes.Distinct().OrderBy(t => t).ToList();

            if (requested.Count > _rules.MaxSlotsPerReservation)
            {
                return SlotSelectionResult.Fail(
                    $"一次最多可預約 {_rules.MaxSlotsPerReservation} 小時，" +
                    $"目前選了 {requested.Count} 小時。");
            }

            // 重新向 TimeSlotService 查詢當天的實際狀態。
            // 與步驟 3 的日曆使用同一個服務，因此不可能出現
            // 「日曆說可預約、送出卻被擋下」的矛盾。
            var daySlots = await _timeSlotService.GetDaySlotsAsync(
                venueId, date, cancellationToken);

            if (daySlots.Count == 0)
            {
                return SlotSelectionResult.Fail("該日期為公休日，無法建立預約。");
            }

            var errors = new List<string>();
            var matched = new List<TimeSlotStatus>();

            // 以索引判斷相鄰，而不是用「相差一小時」。
            // 目前營業時間是 10:00-22:00 連續，兩種判斷結果相同；
            // 但若日後加回午休，12:00 與 13:00 相差一小時卻不相鄰，
            // 用索引才不會把跨越休息時段的選取誤判為連續。
            var indexOf = daySlots
                .Select((slot, index) => (slot.SlotTime, index))
                .ToDictionary(x => x.SlotTime, x => x.index);

            var indices = new List<int>();

            foreach (var slotTime in requested)
            {
                if (!indexOf.TryGetValue(slotTime, out var index))
                {
                    errors.Add($"{slotTime:HH\\:mm} 不在當天的營業時間內。");
                    continue;
                }

                var slot = daySlots[index];

                if (!slot.IsSelectable)
                {
                    errors.Add($"{slot.TimeRangeText} {slot.AvailabilityText}，請重新選擇。");
                    continue;
                }

                matched.Add(slot);
                indices.Add(index);
            }

            if (errors.Count > 0)
            {
                return SlotSelectionResult.Fail(errors);
            }

            // 連續性：排序後的索引必須是連號。
            for (var i = 1; i < indices.Count; i++)
            {
                if (indices[i] != indices[i - 1] + 1)
                {
                    errors.Add("所選時段必須連續。不連續的時段請分次建立預約。");
                    break;
                }
            }

            if (errors.Count > 0)
            {
                return SlotSelectionResult.Fail(errors);
            }

            return SlotSelectionResult.Success(matched);
        }
    }
}