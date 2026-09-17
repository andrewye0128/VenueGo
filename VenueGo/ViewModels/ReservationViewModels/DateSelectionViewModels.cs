using System;
using System.Collections.Generic;
using System.Linq;
using VenueGo.Models.TimeSlots;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 步驟 3「選擇日期」頁面的 ViewModel。
    /// </summary>
    public class SelectDateViewModel
    {
        /// <summary>日曆目前顯示的月份（以該月 1 日表示）。</summary>
        public DateOnly DisplayMonth { get; set; }

        /// <summary>日曆格線。</summary>
        public CalendarViewModel Calendar { get; set; } = new();

        /// <summary>已選的日期。從步驟 4 按上一步回來時用它保持選取。</summary>
        public DateOnly? SelectedDate { get; set; }

        /// <summary>可預約的最早日期。</summary>
        public DateOnly MinDate { get; set; }

        /// <summary>可預約的最晚日期。</summary>
        public DateOnly MaxDate { get; set; }

        /// <summary>步驟 1 已選的會員姓名。</summary>
        public string? MemberName { get; set; }

        /// <summary>步驟 1 已選的會員手機。</summary>
        public string? MemberPhone { get; set; }

        /// <summary>步驟 2 已選的場地名稱。</summary>
        public string? VenueName { get; set; }

        /// <summary>
        /// 可預約範圍的說明文字，顯示在日曆下方。
        /// 由設定值產生而非寫死，設定改了畫面說明會跟著改，不會出現不一致。
        /// </summary>
        public string RangeHintText =>
            $"可預約日期：{MinDate:yyyy/MM/dd} ～ {MaxDate:yyyy/MM/dd}（共 {MaxDate.DayNumber - MinDate.DayNumber + 1} 天）";
    }

    /// <summary>
    /// 月曆格線。
    /// </summary>
    public class CalendarViewModel
    {
        /// <summary>星期標頭，由星期日起，與 System.DayOfWeek 的編碼一致。</summary>
        public static readonly string[] WeekdayHeaders = { "日", "一", "二", "三", "四", "五", "六" };

        /// <summary>顯示的月份。</summary>
        public DateOnly Month { get; init; }

        /// <summary>每一週一列，每列固定七天。</summary>
        public IReadOnlyList<IReadOnlyList<CalendarDayViewModel>> Weeks { get; init; }
            = Array.Empty<IReadOnlyList<CalendarDayViewModel>>();

        /// <summary>上一個月，已超出可預約範圍時為 null（用來停用上一月按鈕）。</summary>
        public DateOnly? PreviousMonth { get; init; }

        /// <summary>下一個月，已超出可預約範圍時為 null。</summary>
        public DateOnly? NextMonth { get; init; }

        /// <summary>月份標題文字。</summary>
        public string MonthText => $"{Month.Year} 年 {Month.Month} 月";

        /// <summary>
        /// 建立月曆格線。
        /// <para>
        /// 格線一律由星期日開始、以完整的七天為一列，
        /// 月初與月末不足的部分補上鄰月的日期並標記為非本月，
        /// 這樣畫面才不會出現缺格而導致欄位對不齊。
        /// </para>
        /// </summary>
        /// <param name="month">要顯示的月份（任一天皆可，會自動取該月 1 日）。</param>
        /// <param name="availability">場地在該月每一天的時段統計。</param>
        /// <param name="minDate">可預約的最早日期。</param>
        /// <param name="maxDate">可預約的最晚日期。</param>
        /// <param name="today">今天的日期。以參數傳入而非在此讀取系統時間，
        /// 確保整張日曆的判斷基準一致。</param>
        public static CalendarViewModel Build(
            DateOnly month,
            IReadOnlyDictionary<DateOnly, DayAvailability> availability,
            DateOnly minDate,
            DateOnly maxDate,
            DateOnly today)
        {
            var firstOfMonth = new DateOnly(month.Year, month.Month, 1);
            var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

            // 往前補到該週的星期日
            var gridStart = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);
            // 往後補到該週的星期六
            var gridEnd = lastOfMonth.AddDays(6 - (int)lastOfMonth.DayOfWeek);

            var weeks = new List<IReadOnlyList<CalendarDayViewModel>>();
            var week = new List<CalendarDayViewModel>();

            for (var date = gridStart; date <= gridEnd; date = date.AddDays(1))
            {
                availability.TryGetValue(date, out var dayAvailability);

                week.Add(new CalendarDayViewModel
                {
                    Date = date,
                    IsInDisplayMonth = date.Month == firstOfMonth.Month
                                       && date.Year == firstOfMonth.Year,
                    IsToday = date == today,
                    IsWithinRange = date >= minDate && date <= maxDate,
                    Availability = dayAvailability
                });

                if (week.Count == 7)
                {
                    weeks.Add(week);
                    week = new List<CalendarDayViewModel>();
                }
            }

            return new CalendarViewModel
            {
                Month = firstOfMonth,
                Weeks = weeks,
                // 只有在鄰月確實含有可預約日期時才提供切換，
                // 否則使用者會翻到一整片灰色的月份，不知道自己做錯什麼。
                PreviousMonth = firstOfMonth.AddDays(-1) >= minDate
                    ? firstOfMonth.AddMonths(-1)
                    : null,
                NextMonth = lastOfMonth.AddDays(1) <= maxDate
                    ? firstOfMonth.AddMonths(1)
                    : null
            };
        }
    }

    /// <summary>
    /// 日曆上的一天。
    /// </summary>
    public class CalendarDayViewModel
    {
        /// <summary>日期。</summary>
        public DateOnly Date { get; init; }

        /// <summary>是否屬於目前顯示的月份。補格用的鄰月日期為 false。</summary>
        public bool IsInDisplayMonth { get; init; }

        /// <summary>是否為今天。</summary>
        public bool IsToday { get; init; }

        /// <summary>是否落在可預約的日期範圍內。</summary>
        public bool IsWithinRange { get; init; }

        /// <summary>該天的時段統計。超出查詢範圍的補格日期為 null。</summary>
        public DayAvailability? Availability { get; init; }

        /// <summary>日期數字，例如 18。</summary>
        public int DayNumber => Date.Day;

        /// <summary>是否可被選取。</summary>
        public bool IsSelectable =>
            IsInDisplayMonth
            && IsWithinRange
            && Availability is not null
            && Availability.HasAvailableSlots;

        /// <summary>
        /// 狀態文字，顯示在日期數字下方。
        /// 不可選時要說明原因，否則櫃檯無法回答會員「為什麼這天不能訂」。
        /// </summary>
        public string? StatusText
        {
            get
            {
                if (!IsInDisplayMonth) return null;
                if (!IsWithinRange) return null;
                if (Availability is null) return null;
                if (Availability.IsClosed) return "公休";
                if (Availability.IsFull) return "額滿";
                return "可預約";
            }
        }

        /// <summary>狀態文字的樣式類別。</summary>
        public string StatusTextClass
        {
            get
            {
                if (Availability is null) return "text-secondary";
                if (Availability.IsClosed) return "text-secondary";
                if (Availability.IsFull) return "text-danger";
                return "text-success";
            }
        }

        /// <summary>
        /// 剩餘時段數的提示文字，作為 title 屬性顯示。
        /// 讓管理員把滑鼠移過去就知道還剩幾格，不必先點進步驟 4。
        /// </summary>
        public string? TooltipText => Availability is null || Availability.IsClosed
            ? null
            : $"剩餘 {Availability.AvailableSlots} / {Availability.TotalSlots} 個時段";
    }
}