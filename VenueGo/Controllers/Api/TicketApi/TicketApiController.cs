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

        public TicketApiController(IMemberTicketQueryService ticketQuery, IWebHostEnvironment env)
        {
            _ticketQuery = ticketQuery;
            _env = env;
        }
    
        // 前台「我的票券」頁 >> GET /api/tickets/mine
        // 回傳這位會員持有或已轉出的票券（已取消的不回傳）
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            [FromQuery] int? testUserId, CancellationToken cancellationToken)
        {

            //int? userId = User.GetUserId();
            int? userId = null; // 先強制為null不抓後台存在Cookie的ID

            // 前台登入還沒串好：只有開發環境允許用 ?testUserId=1 假裝某個會員
            // TODO: 前台登入完成後，整段 testUserId 刪掉
            if (userId is null && _env.IsDevelopment())
                userId = testUserId;

            if (userId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            var tickets = await _ticketQuery.GetMyTicketsAsync(userId.Value, cancellationToken);

            return Ok(ApiResult<List<MemberTicketDto>>.Ok(tickets));
        }

    }

}