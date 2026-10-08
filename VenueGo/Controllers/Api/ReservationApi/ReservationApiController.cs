using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.ReservationDtos;
using VenueGo.Extensions;
using VenueGo.Models.Constants;
using VenueGo.Services.Reservations;

namespace VenueGo.Controllers.Api.ReservationApi
{
    /// <summary>
    /// 前台會員的預約 API。
    /// <para>
    /// Controller 只負責轉接：決定是哪位會員、收下查詢條件、包成 ApiResult。
    /// 查詢、篩選、排序、分頁都在 IReservationQueryService。
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/reservations")]
    public class ReservationApiController : ControllerBase
    {
        private readonly IReservationQueryService _reservationQuery;
        private readonly IWebHostEnvironment _env;

        public ReservationApiController(
            IReservationQueryService reservationQuery,
            IWebHostEnvironment env)
        {
            _reservationQuery = reservationQuery;
            _env = env;
        }

        /// <summary>我的預約列表（篩選、分頁）</summary>
        /// <remarks>
        /// 前台會員中心「我的預約」頁使用，不含已作廢的預約。
        ///
        /// 排序：預約日期新到舊 → 開始時間晚到早 → 預約編號大
        ///
        /// 開發環境還沒有會員登入時，可以帶 testUserId 假裝某位會員。
        /// </remarks>
        /// <param name="query">篩選條件與分頁</param>
        /// <param name="testUserId">開發用：假裝某位會員（正式環境無效）</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功；查無資料時 items 是空陣列</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        [HttpGet]
        [ProducesResponseType<ApiResult<PagedResult<MyReservationDto>>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine(
            [FromQuery] MyReservationQuery query,
            [FromQuery] int? testUserId,
            CancellationToken cancellationToken)
        {
            var userId = ResolveMemberUserId(testUserId);
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var result = await _reservationQuery.GetMyReservationsAsync(
                userId.Value, query, cancellationToken);

            return Ok(ApiResult<PagedResult<MyReservationDto>>.Ok(result));
        }

        /// <summary>
        /// 決定是哪位會員。
        /// <para>
        /// 1. 登入的是會員 → 用登入的 UserId。
        /// 2. 開發環境 → 用網址的 testUserId（前台會員登入還沒完成）。
        /// 3. 其他 → null（沒登入）。
        /// </para>
        /// <para>
        /// 只認「會員」角色：員工登入後台後，Cookie 裡是員工的 UserId，
        /// 不能被當成會員查預約（TicketApiController 也是因此不讀 Cookie）。
        /// </para>
        /// <para>
        /// TODO：前台會員登入完成後，刪掉 testUserId（參數和第 2 點）。
        /// </para>
        /// </summary>
        private int? ResolveMemberUserId(int? testUserId)
        {
            if (User.IsInRole(RoleNames.Member))
            {
                return User.GetUserId();
            }

            if (_env.IsDevelopment())
            {
                return testUserId;
            }

            return null;
        }
    }
}