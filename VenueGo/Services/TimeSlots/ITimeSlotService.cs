using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.Models.TimeSlots;

namespace VenueGo.Services.TimeSlots
{
    /// <summary>
    /// 時段服務：唯一負責「某場地某天有哪些時段、各自是什麼狀態」的地方。
    /// <para>
    /// 【為何要集中】這件事至少有三個地方要算：步驟 3 的日曆要數「當天還剩幾格」、
    /// 步驟 4 的時段表要畫出每一格、期末的會員前台還會再算一次。
    /// 若各自實作，只要對「一天有幾格」的理解不同，就會出現
    /// 「日曆說還有 2 格可預約，點進去時段表卻全滿」這種無法解釋的畫面，
    /// 而且兩邊程式各自看起來都沒錯，極難排查。
    /// </para>
    /// <para>
    /// 【為何抽介面】時段規則已經改過一次（從分三段的營業時間改為 10:00-22:00 連續），
    /// 期末很可能再改（加回午休、平日假日不同、各場地不同營業時間）。
    /// 有介面的話，改規則就是換一個實作類別、Program.cs 改一行，
    /// 三處呼叫端完全不用動。
    /// </para>
    /// </summary>
    public interface ITimeSlotService
    {
        /// <summary>
        /// 取得某場地某天的所有時段及其狀態。
        /// <para>
        /// 回傳的清單一定涵蓋當天營業時間內的每一格（含不可選的），
        /// 而不是只回傳可預約的那些。原因是畫面需要把「已預約」「維護中」
        /// 也畫出來，櫃檯才能向會員解釋為什麼訂不到。
        /// 當天公休時回傳空清單。
        /// </para>
        /// </summary>
        /// <param name="venueId">場地 Id。時段可用性是「場地 + 日期 + 時段」綁在一起的，缺一不可。</param>
        /// <param name="date">要查詢的日期。</param>
        Task<IReadOnlyList<TimeSlotStatus>> GetDaySlotsAsync(
            int venueId, DateOnly date, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得某場地一段日期範圍內每一天的時段統計，供日曆顯示「可預約 / 額滿」。
        /// <para>
        /// 以一次範圍查詢處理整個月，避免對每一天各查一次資料庫。
        /// 回傳的字典一定包含範圍內的每一天，查無資料的日期會是公休狀態，
        /// 呼叫端不需要處理找不到鍵值的情況。
        /// </para>
        /// </summary>
        /// <param name="fromDate">起始日期（含）。</param>
        /// <param name="toDate">結束日期（含）。</param>
        Task<IReadOnlyDictionary<DateOnly, DayAvailability>> GetRangeAvailabilityAsync(
            int venueId, DateOnly fromDate, DateOnly toDate,
            CancellationToken cancellationToken = default);
    }
}
