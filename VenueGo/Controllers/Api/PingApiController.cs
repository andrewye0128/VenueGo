using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace VenueGo.Controllers.Api
{
    [ApiController]
    [Route("api/ping")]
    public class PingApiController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { message = "pong", time = DateTime.Now });
        }
    }
}
