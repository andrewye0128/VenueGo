using System;
using System.Collections.Generic;
using System.Linq;
using VenueGo.Models.ReservationModels;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 步驟 4「選擇時段」頁面的 ViewModel。
    /// </summary>
    public class SelectSlotsViewModel
    {
        /// <summary>當天的所有時段按鈕，依時間排序。</summary>
        public IReadOnlyList<SlotButtonViewModel> Slots { get; set; }
            = Array.Empty<SlotButtonViewModel>();

        /// <summary>一次可選的時段上限，供畫面提示與 JavaScript 使用。</summary>
        public int MaxSlots { get; set; }

        /// <summary>目前的計價結果。未選任何時段時為空結果。</summary>
        public PricingResult Pricing { get; set; } = PricingResult.Empty();

        /// <summary>步驟 1 已選的會員姓名。</summary>
        public string? MemberName { get; set; }

        /// <summary>步驟 1 已選的會員手機。</summary>
        public string? MemberPhone { get; set; }

        /// <summary>步驟 2 已選的場地名稱。</summary>
        public string? VenueName { get; set; }

        /// <summary>步驟 3 已選的使用日期。</summary>
        public DateOnly? BookingDate { get; set; }

        /// <summary>已選的時段數。</summary>
        public int SelectedCount => Slots.Count(s => s.IsSelected);

        /// <summary>是否已選了時段。</summary>
        public bool HasSelection => SelectedCount > 0;

        /// <summary>日期顯示文字，含星期。</summary>
        public string BookingDateText
        {
            get
            {
                if (BookingDate is null) return "-";
                string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
                var date = BookingDate.Value;
                return $"{date:yyyy/MM/dd}（{weekdays[(int)date.DayOfWeek]}）";
            }
        }

        /// <summary>
        /// 已選時段的整段區間文字，例如「19:00 - 22:00」。
        /// 因為只允許連續時段，所以頭尾兩端就能表達整段。
        /// </summary>
        public string SelectedRangeText
        {
            get
            {
                var selected = Slots.Where(s => s.IsSelected).ToList();
                if (selected.Count == 0) return "-";

                var start = selected.Min(s => s.SlotTime);
                var end = selected.Max(s => s.SlotTime).AddHours(1);
                return $"{start:HH\\:mm} - {end:HH\\:mm}";
            }
        }

        /// <summary>當天是否完全沒有可預約的時段。</summary>
        public bool IsDayFull => Slots.Count > 0 && Slots.All(s => !s.IsSelectable);

        /// <summary>當天是否公休。</summary>
        public bool IsDayClosed => Slots.Count == 0;
    }

    /// <summary>
    /// 一個時段按鈕。
    /// </summary>
    public class SlotButtonViewModel
    {
        /// <summary>時段起始時間。</summary>
        public TimeOnly SlotTime { get; init; }

        /// <summary>顯示文字，例如「19:00 - 20:00」。</summary>
        public string TimeRangeText { get; init; } = string.Empty;

        /// <summary>表單送出的值，格式 HH:mm。</summary>
        public string FormValue { get; init; } = string.Empty;

        /// <summary>是否可被選取。</summary>
        public bool IsSelectable { get; init; }

        /// <summary>是否為目前已選的時段。</summary>
        public bool IsSelected { get; init; }

        /// <summary>不可選時的狀態文字，例如「已預約」「維護中」。</summary>
        public string AvailabilityText { get; init; } = string.Empty;

        /// <summary>單價。查無計價規則時為 null。</summary>
        public int? UnitPrice { get; init; }

        /// <summary>是否為尖峰時段。</summary>
        public bool IsPeak { get; init; }

        /// <summary>
        /// 按鈕第二行的文字。
        /// 可選時顯示單價與尖峰標註（管理員需要當場向會員報價）；
        /// 不可選時顯示原因，讓櫃檯能回答「為什麼這個時段訂不到」。
        /// </summary>
        public string SecondaryText
        {
            get
            {
                if (!IsSelectable) return AvailabilityText;
                if (UnitPrice is null) return "價格未設定";
                return IsPeak ? $"NT$ {UnitPrice:N0} 尖峰" : $"NT$ {UnitPrice:N0}";
            }
        }

        /// <summary>
        /// 由 TimeSlotStatus 建立按鈕資料。
        /// </summary>
        /// <param name="slot">時段狀態。</param>
        /// <param name="isSelected">是否為已選的時段。</param>
        public static SlotButtonViewModel FromStatus(TimeSlotStatus slot, bool isSelected) => new()
        {
            SlotTime = slot.SlotTime,
            TimeRangeText = slot.TimeRangeText,
            FormValue = slot.FormValue,
            IsSelectable = slot.IsSelectable,
            IsSelected = isSelected,
            AvailabilityText = slot.AvailabilityText,
            UnitPrice = slot.UnitPrice,
            IsPeak = slot.IsPeak
        };
    }
}
