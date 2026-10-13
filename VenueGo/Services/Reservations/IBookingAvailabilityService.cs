using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.Dtos.BookingDtos;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 前台場地預約頁的查詢服務：某天各場地的時段、一段日期每天剩幾格。
    /// <para>
    /// 【為何不直接在 Controller 呼叫 ITimeSlotService】
    /// 要先找出「哪些場地可以預約」（場地、運動都要啟用），再一次查這些場地的時段、
    /// 轉成前端要的格式。這是一段完整的邏輯，放在 Service 裡，Controller 只負責接參數、回傳結果。
    /// </para>
    /// <para>
    /// 【查詢次數】使用 ITimeSlotService 的批次方法（一次查多個場地），
    /// 不論幾個場地，每次請求只查資料庫 4～5 次。
    /// </para>
    /// </summary>
    public interface IBookingAvailabilityService
    {
        /// <summary>
        /// 這個日期是否在前台會員可預約的範圍內（今天～30 天後，用網站時間判斷今天）。
        /// </summary>
        /// <param name="date">要判斷的日期。</param>
        bool IsWithinBookingRange(DateOnly date);

        /// <summary>
        /// 某天各場地的時段。只回傳啟用中的場地與運動。
        /// </summary>
        /// <param name="date">要查詢的日期。</param>
        /// <param name="sportTypeId">運動類型；null 表示全部運動。</param>
        /// <param name="cancellationToken">使用者中斷請求（例如關閉頁面）時，取消進行中的查詢。</param>
        Task<BookingAvailabilityDto> GetDayAvailabilityAsync(
            DateOnly date,
            int? sportTypeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 一段日期內，每天所有場地加總還剩幾格。範圍內的每一天都一定會出現。
        /// </summary>
        /// <param name="fromDate">起始日期（含）。</param>
        /// <param name="toDate">結束日期（含）。</param>
        /// <param name="sportTypeId">運動類型；null 表示全部運動。</param>
        /// <param name="period">時段篩選：morning／afternoon／evening；null 或不認得的值表示整天。</param>
        /// <param name="cancellationToken">使用者中斷請求（例如關閉頁面）時，取消進行中的查詢。</param>
        Task<IReadOnlyList<DayAvailabilitySummaryDto>> GetDaySummariesAsync(
            DateOnly fromDate,
            DateOnly toDate,
            int? sportTypeId,
            string? period,
            CancellationToken cancellationToken = default);
    }
}