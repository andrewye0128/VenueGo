using Microsoft.AspNetCore.Authentication.JwtBearer; 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.ReservationDtos;
using VenueGo.Models.Constants;
using VenueGo.Services.Auth;
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
        private readonly ICurrentUserService _currentUser;

        public ReservationApiController(
            IReservationQueryService reservationQuery,
            ICurrentUserService currentUser)
        {
            _reservationQuery = reservationQuery;
            _currentUser = currentUser;
        }

        /// <summary>我的預約列表（篩選、分頁）</summary>
        /// <remarks>
        /// 前台會員中心「我的預約」頁使用，不含已作廢的預約。
        ///
        /// 需要會員的 JWT（Authorization: Bearer ...）。
        ///
        /// 排序：預約日期新到舊 → 開始時間晚到早 → 預約編號大到小。
        /// </remarks>
        /// <param name="query">篩選條件與分頁</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功；查無資料時 items 是空陣列</response>
        /// <response code="401">沒有登入或 token 失效</response>
        /// <response code="403">登入的不是會員</response>
        [HttpGet]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Roles = RoleNames.Member)]
        [ProducesResponseType<ApiResult<PagedResult<MyReservationDto>>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMine(
            [FromQuery] MyReservationQuery query,
            CancellationToken cancellationToken)
        {
            // [Authorize] 已經擋掉沒登入的人；這裡是保險（token 有效但沒有 UserId）
            var userId = _currentUser.UserId;
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var result = await _reservationQuery.GetMyReservationsAsync(
                userId.Value, query, cancellationToken);

            return Ok(ApiResult<PagedResult<MyReservationDto>>.Ok(result));
        }
    }
}