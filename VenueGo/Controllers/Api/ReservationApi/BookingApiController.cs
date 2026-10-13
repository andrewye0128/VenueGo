using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VenueGo.Dtos;
using VenueGo.Dtos.BookingDtos;
using VenueGo.Services.Reservations;

namespace VenueGo.Controllers.Api.ReservationApi
{
    /// <summary>
    /// 前台場地預約頁的 API：查某天各場地的時段、查一段日期每天剩幾格。
    /// <para>
    /// 全部都不需要登入：還沒登入的人也能先看有沒有空位，
    /// 按「下一步：填寫資料」時才要求登入。
    /// </para>
    /// <para>
    /// Controller 只負責檢查日期、包成 ApiResult；查詢與合併在 IBookingAvailabilityService。
    /// </para>
    /// </summary>
    /// 
    [ApiController]
    [Route("api/booking")]
    [AllowAnonymous]   // 刻意不用登入（寫出來，避免被誤以為是忘了加 [Authorize]）
    public class BookingApiController : ControllerBase
    {
        private readonly IBookingAvailabilityService _booking;

        /// <param name="booking">前台場地預約頁的查詢服務。</param>
        public BookingApiController(IBookingAvailabilityService booking)
        {
            _booking = booking;
        }

        /// <summary>某天各場地的時段</summary>
        /// <remarks>
        /// 只回傳啟用中的場地與運動。sportTypeId 不傳表示全部運動。
        ///
        /// slotTime 格式是 "18:00"；availability：0 可預約、1 已預約、2 維護中、3 已過時間。
        /// </remarks>
        /// <param name="date">日期（yyyy-MM-dd），必須在今天～30 天後</param>
        /// <param name="sportTypeId">運動類型；不傳表示全部運動</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功；公休日時每個場地的 slots 是空陣列</response>
        /// <response code="400">日期格式錯誤（ValidationFailed）或不在可預約範圍（DateOutOfRange）</response>
        /// 
        [HttpGet("availability")]
        [ProducesResponseType<ApiResult<BookingAvailabilityDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAvailability(
            [FromQuery, BindRequired] DateOnly date,
            [FromQuery] int? sportTypeId,
            CancellationToken cancellationToken)
        {
            if (!_booking.IsWithinBookingRange(date))
            {
                return BadRequest(ApiResult.Fail("這個日期不開放預約", "DateOutOfRange"));
            }

            var data = await _booking.GetDayAvailabilityAsync(date, sportTypeId, cancellationToken);
            return Ok(ApiResult<BookingAvailabilityDto>.Ok(data));
        }

        /// <summary>一段日期每天剩幾格</summary>
        /// <remarks>
        /// 前台預約頁的日期列使用。範圍內的每一天都會出現，沒有空位的日子是 0。
        ///
        /// period：morning（～12:00）、afternoon（12:00～18:00）、evening（18:00～）；不傳表示整天。
        /// </remarks>
        /// <param name="from">起始日期（yyyy-MM-dd，含）</param>
        /// <param name="to">結束日期（yyyy-MM-dd，含）；最多 31 天</param>
        /// <param name="sportTypeId">運動類型；不傳表示全部運動</param>
        /// <param name="period">時段篩選；不傳表示整天</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功</response>
        /// <response code="400">日期格式錯誤（ValidationFailed）或不在可預約範圍（DateOutOfRange）</response>
        /// 
        [HttpGet("availability/summary")]
        [ProducesResponseType<ApiResult<IReadOnlyList<DayAvailabilitySummaryDto>>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAvailabilitySummary(
            [FromQuery, BindRequired] DateOnly from,
            [FromQuery, BindRequired] DateOnly to,
            [FromQuery] int? sportTypeId,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            // 開始不能晚於結束、最多 31 天（DayNumber 相減 = 相差幾天）、兩頭都要在可預約範圍內
            if (from > to
                || to.DayNumber - from.DayNumber > 30
                || !_booking.IsWithinBookingRange(from)
                || !_booking.IsWithinBookingRange(to))
            {
                return BadRequest(ApiResult.Fail("這段日期不開放預約", "DateOutOfRange"));
            }

            var data = await _booking.GetDaySummariesAsync(
                from, to, sportTypeId, period, cancellationToken);
            return Ok(ApiResult<IReadOnlyList<DayAvailabilitySummaryDto>>.Ok(data));
        }
    }
}
