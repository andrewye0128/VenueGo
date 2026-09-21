using System;
using VenueGo.Models.Enums;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 單一時段的狀態。由 ITimeSlotService 計算產生。
    /// <para>
    /// 放在 Models 而非 ViewModels，是因為它不只給畫面用：
    /// 步驟 4 的時段表要用它畫按鈕、計價要用它的單價、
    /// 最終寫入資料庫前的驗證也要用它確認時段仍可預約。
    /// </para>
    /// </summary>
    public class TimeSlotStatus
    {
        /// <summary>時段的起始時間，例如 19:00。對應 ReservationSlots.SlotTime。</summary>
        public TimeOnly SlotTime { get; init; }

        /// <summary>時段的結束時間。固定為起始時間加一小時。</summary>
        public TimeOnly EndTime => SlotTime.AddHours(1);

        /// <summary>可用狀態。</summary>
        public SlotAvailability Availability { get; init; }

        /// <summary>
        /// 這一格的單價。依尖峰離峰規則計算後的結果，
        /// 查無計價規則時為 null。
        /// </summary>
        public int? UnitPrice { get; init; }

        /// <summary>是否為尖峰時段。供畫面標示使用。</summary>
        public bool IsPeak { get; init; }

        /// <summary>是否可被選取。</summary>
        public bool IsSelectable => Availability == SlotAvailability.Available;

        /// <summary>顯示文字，例如「19:00 - 20:00」。</summary>
        public string TimeRangeText => $"{SlotTime:HH\\:mm} - {EndTime:HH\\:mm}";

        /// <summary>狀態顯示文字。</summary>
        public string AvailabilityText => Availability.GetDisplayName();

        /// <summary>單價顯示文字。</summary>
        public string UnitPriceText => UnitPrice.HasValue ? $"NT$ {UnitPrice:N0}" : "未設定";


        /// <summary>
        /// 表單送出用的值，格式 HH:mm，例如 19:00。
        /// 與 Controller 的 TimeOnly 參數繫結對應。
        /// </summary>
        public string FormValue => SlotTime.ToString("HH\\:mm");
    }

    /// <summary>
    /// 某一天的時段統計。供日曆顯示「可預約 / 額滿」使用。
    /// <para>
    /// 日曆一次要顯示一整個月，若對每一天都呼叫 GetDaySlotsAsync，
    /// 會產生三十幾次資料庫往返。因此另外提供這個較輕量的統計結果，
    /// 由一次範圍查詢算出整個月。
    /// </para>
    /// </summary>
    public class DayAvailability
    {
        /// <summary>日期。</summary>
        public DateOnly Date { get; init; }

        /// <summary>當天營業時間內的總時段數。公休日為 0。</summary>
        public int TotalSlots { get; init; }

        /// <summary>可預約的時段數。</summary>
        public int AvailableSlots { get; init; }

        /// <summary>當天是否公休或未設定營業時間。</summary>
        public bool IsClosed => TotalSlots == 0;

        /// <summary>
        /// 當天是否已無可預約時段。公休日不算額滿，因為那是兩件不同的事，
        /// 畫面上要顯示不同的文字。
        /// </summary>
        public bool IsFull => !IsClosed && AvailableSlots == 0;

        /// <summary>是否還有時段可預約。</summary>
        public bool HasAvailableSlots => AvailableSlots > 0;
    }
}