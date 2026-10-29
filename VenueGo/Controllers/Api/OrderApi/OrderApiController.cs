using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Constants;
using VenueGo.Dtos;
using VenueGo.Dtos.OrderDtos;
using VenueGo.Services.Auth;
using VenueGo.Services.Orders;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.Payments;

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
        private readonly IEcpayCheckoutService _checkout;

        public OrderApiController(
            IMemberOrderQueryService orderQuery,
            ICurrentUserService currentUser,
            IEcpayCheckoutService checkout)
        {
            _orderQuery = orderQuery;
            _currentUser = currentUser;
            _checkout = checkout;
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

        /// <summary>開始線上付款（綠界）</summary>
        /// <remarks>
        /// 檢查可以付款後，付款改成「付款處理中」，回傳要送給綠界的表單（actionUrl＋fields）。
        /// 前端用表單 POST 送出，畫面就跳到綠界的信用卡付款頁。
        ///
        /// body 不用傳東西：金額、網址都由後端決定。
        ///
        /// 回傳成功不代表已付款：付款結果由綠界通知（3-C-4）決定。
        /// 付款處理中不能再開新的付款單（不知道上一筆有沒有扣款）。
        ///
        /// errorCode：NotFound、NotOnlinePayment、AlreadyPaid、InProgress、NotPayable、
        /// PastStartDeadline、InvalidAmount、Conflict、NotConfigured、TradeNoUnavailable
        /// </remarks>
        /// <param name="orderId">訂單 Id（Orders.OrderId）</param>
        /// <param name="cancellationToken">使用者中斷請求時取消</param>
        /// <response code="200">成功，data 是 actionUrl＋fields</response>
        /// <response code="401">沒有登入或 token 失效</response>
        /// <response code="403">登入的不是會員</response>
        /// <response code="404">NotFound：不存在、不是自己的</response>
        /// <response code="409">目前不能付款（errorCode 說明原因）</response>
        /// <response code="503">NotConfigured、TradeNoUnavailable：線上付款暫時無法使用</response>
        [HttpPost("{orderId:int}/payment/start")]
        [ProducesResponseType<ApiResult<StartPaymentDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> StartPayment(int orderId, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId;
            if (userId is null)
            {
                return Unauthorized(ApiResult.Fail("請先登入", "NotLoggedIn"));
            }

            var result = await _checkout.StartAsync(userId.Value, orderId, cancellationToken);

            // errorCode 直接用 Outcome 的名稱（InProgress、PastStartDeadline⋯），前端用它判斷，不比對中文字
            var errorCode = result.Outcome.ToString();

            return result.Outcome switch
            {
                StartPaymentOutcome.Started => Ok(ApiResult<StartPaymentDto>.Ok(new StartPaymentDto
                {
                    ActionUrl = result.ActionUrl!,
                    Fields = result.Fields!
                })),

                StartPaymentOutcome.NotFound => NotFound(ApiResult.Fail(result.Message, errorCode)),

                // 不是客人的問題，是網站暫時不能線上付款
                StartPaymentOutcome.NotConfigured or StartPaymentOutcome.TradeNoUnavailable
                    => StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResult.Fail(result.Message, errorCode)),

                // 其餘都是「訂單目前的狀態不能付款」
                _ => Conflict(ApiResult.Fail(result.Message, errorCode))
            };
        }
    }
}