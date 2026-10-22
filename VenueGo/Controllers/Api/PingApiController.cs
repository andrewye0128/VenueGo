using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;

namespace VenueGo.Controllers.Api
{
    [ApiController]
    [Route("api/ping")]
    public class PingApiController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // ② 把原本的 return 改成用 ApiResult 包起來
            // 原本：return Ok(new { message = "pong", time = DateTime.Now });
#pragma warning disable RS0030 // 回傳伺服器實際時間
            return Ok(ApiResult<object>.Ok(new { reply = "pong", time = DateTime.Now }));
#pragma warning restore RS0030
        }
    }
}
