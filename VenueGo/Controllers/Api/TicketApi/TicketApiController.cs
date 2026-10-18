using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.TicketDtos;
using VenueGo.Extensions;
using VenueGo.Helpers;
using VenueGo.Services.Ticket;
using VenueGo.ViewModels;
namespace VenueGo.Controllers.Api.TicketApi
{

    // 前台會員的票券 API
    // Controller 只負責轉接：誰在查、回傳格式；狀態怎麼換算全部在 MemberTicketQueryService
    [ApiController]
    [Route("api/tickets")]
    public class TicketApiController : ControllerBase
    {
        private readonly IMemberTicketQueryService _ticketQuery;
        private readonly IWebHostEnvironment _env;
        private readonly IEntryTicketService _entryTicketService;

        public TicketApiController(IMemberTicketQueryService ticketQuery, IWebHostEnvironment env, IEntryTicketService entryTicketService)
        {
            _ticketQuery = ticketQuery;
            _entryTicketService = entryTicketService;
            _env = env;
        }

        // 前台「我的票券」頁 >> GET /api/tickets/mine
        // 回傳這位會員持有或已轉出的票券（已取消的不回傳）
        [Authorize(
       AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            [FromQuery] int? testUserId, CancellationToken cancellationToken)
        {

            int? userId = User.GetUserId();
            //int? userId = null; // 先強制為null不抓後台存在Cookie的ID

            // 前台登入還沒串好：只有開發環境允許用 ?testUserId=1 假裝某個會員
            // TODO: 前台登入完成後，整段 testUserId 刪掉
            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            var tickets = await _ticketQuery.GetMyTicketsAsync(userId.Value, cancellationToken);

            return Ok(ApiResult<List<MemberTicketDto>>.Ok(tickets));
        }


        // 前台「使用票券」視窗 >> GET /api/tickets/{id}/qr
        // 回傳動態 QR（每 30 秒換一次），只有票的現在持有人拿得到
        [Authorize(
      AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("{id:int}/qr")]
        public async Task<IActionResult> GetQr(
            int id, [FromQuery] int? testUserId, CancellationToken cancellationToken)
        {
            int? userId = User.GetUserId();

            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            // QR 每 30 秒就變，不能讓瀏覽器或中間的代理快取舊的
            Response.Headers.CacheControl = "no-store";

            TicketQrDto? dto = await _ticketQuery.GetQrAsync(id, userId.Value, cancellationToken);

            // 統一錯訊息
            if (dto == null)
                return NotFound(ApiResult.Fail("此票券目前無法出示 QR", "QR_UNAVAILABLE"));

            return Ok(ApiResult<TicketQrDto>.Ok(dto));
        }


        [Authorize(
      AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        //POST /api/tickets/{id}/transfer
        [HttpPost("{id:int}/transfer")]
        public async Task<IActionResult> CreateCode(int id, [FromQuery] int? testUserId)
        {
            int? userId = User.GetUserId();

            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            TransferCodeResult result = await _entryTicketService.TransferCreateAsync(id, userId!.Value);

            if (!result.Success)
            {
                return BadRequest(ApiResult.Fail(result.Reason ?? "產生驗證碼失敗", "TRANSFER_FAILED"));
            }

            var dto = new TransferCodeDto
            {
                Code = result.Code!,
                ExpiresAt = result.ExpiresAt!.Value
            };

            return Ok(ApiResult<TransferCodeDto>.Ok(dto));
        }

        [Authorize(
     AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        //DELETE /api/tickets/{id}/transfer
        [HttpDelete("{id:int}/transfer")]
        public async Task<IActionResult> DeleteTransfer(int id, [FromQuery] int? testUserId)
        {
            int? userId = User.GetUserId();

            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            TicketTransferResult result = await _entryTicketService.TransferCancelAsync(id, userId!.Value);

            if (!result.Success)
            {
                return BadRequest(ApiResult.Fail(result.Reason ?? "取消轉讓失敗", "CANCEL_FAILED"));
            }

            return Ok(ApiResult.Ok("已取消轉讓"));
        }


        [Authorize(
     AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        // POST /api/tickets/redeem
        [HttpPost("redeem")]
        public async Task<IActionResult> RedeemTransfer([FromBody] RedeemTicketRequest request, [FromQuery] int? testUserId)
        {
            int? userId = User.GetUserId();

            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            TicketTransferResult result = await _entryTicketService.TransferRedeemAsync(request.Code, userId!.Value);

            if (!result.Success)
            {
                return BadRequest(ApiResult.Fail(result.Reason ?? "接收轉讓失敗", "REDEEM_FAILED"));
            }

            return Ok(ApiResult.Ok("領取成功"));
        }
    }
}