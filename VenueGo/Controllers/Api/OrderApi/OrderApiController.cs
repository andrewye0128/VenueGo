using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Constants;
using VenueGo.Dtos;
using VenueGo.Dtos.OrderDtos;
using VenueGo.Services.Auth;
using VenueGo.Services.Orders;

namespace VenueGo.Controllers.Api.OrderApi
{
    /// <summary>
    /// 前台會員的訂單 API（會員中心「訂單與付款」頁）。
    /// <para>
    /// Controller 只負責轉接：決定是哪位會員、收下查詢條件、包成 ApiResult。
    /// 查詢、篩選、排序、分頁都在 IMemberOrderQueryService。
    /// </para>
    /// <para>
    /// [Authorize] 寫在 class 上：這裡每一支 API 都要會員登入，寫一次就好，之後新增的也不會忘記加。
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Roles = RoleNames.Member)]
    public class OrderApiController : ControllerBase
    {
        private readonly IMemberOrderQueryService _orderQuery;
        private readonly ICurrentUserService _currentUser;

        public OrderApiController(IMemberOrderQueryService orderQuery, ICurrentUserService currentUser)
        {
            _orderQuery = orderQuery;
            _currentUser = currentUser;
        }

        /// <summary>我的訂單列表（篩選、分頁）</summary>
        /// <remarks>
        /// 只回傳有預約、預約沒有作廢的訂單。
        ///
        /// 需要會員的 JWT（Authorization: Bearer ...）。
        ///
        /// 排序：下單時間新到舊 → 訂單 Id 大到小。
        /// </remarks>
        /// <param name="query">篩選條件與分頁</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        /// <response code="200">成功；查無資料時 items 是空陣列</response>
        /// <response code="401">沒有登入或 token 失效</response>
        /// <response code="403">登入的不是會員</response>
        [HttpGet]
        [ProducesResponseType<ApiResult<PagedResult<MyOrderDto>>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMine(
            [FromQuery] MyOrderQuery query,
            CancellationToken cancellationToken)
        {
            // [Authorize] 已經擋掉沒登入的人；這裡是保險（token 有效但沒有 UserId）
            var userId = _currentUser.UserId;
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var result = await _orderQuery.GetMyOrdersAsync(userId.Value, query, cancellationToken);

            return Ok(ApiResult<PagedResult<MyOrderDto>>.Ok(result));
        }

        /// <summary>一筆訂單的明細</summary>
        /// <remarks>
        /// 列表的全部欄位，再加上人數、發票、費用明細（lines）、付款紀錄（payments）。
        ///
        /// 不是自己的訂單也回 404（不回 403）：回 403 等於告訴對方「這張訂單存在」，
        /// 別人就能一個一個號碼試，猜出網站有多少張訂單。
        /// </remarks>
        /// <param name="orderId">訂單 Id（Orders.OrderId，不是 VG 開頭的訂單編號）</param>
        /// <param name="cancellationToken">使用者關閉視窗時取消查詢</param>
        /// <response code="200">成功</response>
        /// <response code="401">沒有登入或 token 失效</response>
        /// <response code="403">登入的不是會員</response>
        /// <response code="404">NotFound：不存在、不是自己的、預約已作廢</response>
        [HttpGet("{orderId:int}")]
        [ProducesResponseType<ApiResult<MyOrderDetailDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOne(int orderId, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId;
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var order = await _orderQuery.GetMyOrderAsync(userId.Value, orderId, cancellationToken);
            if (order is null)
            {
                return NotFound(ApiResult.Fail("找不到這筆訂單", "NotFound"));
            }

            return Ok(ApiResult<MyOrderDetailDto>.Ok(order));
        }
    }
}