using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Constants;
using VenueGo.Dtos;
using VenueGo.Dtos.ReservationDtos;
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

        private readonly IMemberBookingService _memberBooking;
        private readonly ICurrentUserService _currentUser;

        public ReservationApiController(
            IReservationQueryService reservationQuery,
            IMemberBookingService memberBooking,
            ICurrentUserService currentUser)
        {
            _reservationQuery = reservationQuery;
            _memberBooking = memberBooking;
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


        /// <summary>建立預約（會員自助預約）</summary>
        /// <remarks>
        /// 前台「填寫資料」頁按「確認預約」時呼叫。
        ///
        /// 需要會員的 JWT（Authorization: Bearer ...）。預約記在登入的會員名下，不從 body 收會員。
        ///
        /// body 沒有金額：後端重新查證時段、重新計價。
        /// 建立後一律是 待確認／待付款／未付款；付款期限依付款方式計算
        /// （線上＝現在＋30 分鐘但不晚於時段開始；現場＝時段開始前 1 天，距離使用日 3 天以上才能選）。
        /// </remarks>
        /// <param name="request">預約內容（slotTimes 用 "17:00" 格式）</param>
        /// <param name="cancellationToken">使用者中斷請求時取消</param>
        /// <response code="201">建立成功，回傳完成頁要顯示的資料</response>
        /// <response code="400">DateOutOfRange、VenueNotBookable、SlotUnavailable、PaymentMethodNotAllowed、TermsVersionMismatch、InvalidInput、ValidationFailed</response>
        /// <response code="401">沒有登入或 token 失效</response>
        /// <response code="403">登入的不是會員</response>
        /// <response code="409">SlotTaken：寫入的那一刻時段被別人搶先預約</response>
        [HttpPost]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Roles = RoleNames.Member)]
        [ProducesResponseType<ApiResult<CreateReservationResultDto>>(StatusCodes.Status201Created)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(
            [FromBody] CreateReservationRequest request,
            CancellationToken cancellationToken)
        {
            // [Authorize] 已經擋掉沒登入的人；這裡是保險（token 有效但沒有 UserId）
            var userId = _currentUser.UserId;
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var result = await _memberBooking.CreateAsync(userId.Value, request, cancellationToken);

            if (result.IsSuccess)
            {
                // 201 Created：新增了一筆資料。目前沒有「查單筆預約」的 API，所以不帶 Location 標頭
                return StatusCode(StatusCodes.Status201Created,
                    ApiResult<CreateReservationResultDto>.Ok(result.Data!));
            }

            // 時段在寫入那一刻被搶走是「跟別人衝突」→ 409；其他都是送來的資料不符合規則 → 400
            var body = ApiResult.Fail(result.Message!, result.ErrorCode);
            return result.ErrorCode == BookingErrorCodes.SlotTaken
                ? Conflict(body)
                : BadRequest(body);
        }

    }
}