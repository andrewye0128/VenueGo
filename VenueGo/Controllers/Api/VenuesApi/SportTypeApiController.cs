using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.VenueDtos;
using VenueGo.Models.VenueModels;

namespace VenueGo.Controllers.Api.VenuesApi
{
    [Route("api/sport-types")]
    [ApiController]
    public class SportTypeApiController : ControllerBase
    {
        //API >> 取得運動類型清單
        [HttpGet]
        public IActionResult GetSportTypes()
        {
            //呼叫Factory取得運動類型資料
            CSportTypeFactory Factory = new CSportTypeFactory();
            List<SportTypeOptionDto> sportTypes = Factory.QuerySportTypeOptions();

            //包成 ApiResult 格式回傳
            return Ok(ApiResult<List<SportTypeOptionDto>>.Ok(sportTypes));
        }
    }
}
