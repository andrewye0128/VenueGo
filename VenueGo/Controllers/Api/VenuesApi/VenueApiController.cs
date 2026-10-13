using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.VenueDtos;
using VenueGo.Models.VenueModels;

namespace VenueGo.Controllers.Api.VenuesApi
{
    //場地模組(HungYu)提供給前台(Front-Web)的 API
    //只回傳 JSON,不需要 View 的功能,所以繼承 ControllerBase(不是 Controller)
    //Controller 只負責轉接:商業規則(哪些運動類型要顯示、價格怎麼判斷)全部在 Factory
    [ApiController]
    [Route("api/venues")]
    public class VenueApiController : ControllerBase
    {
        //提供前台首頁的"場館介紹"資料 >> GET /api/venues
        //回傳所有場地資料給圖卡顯示用
        [HttpGet]
        public IActionResult GetVenues()
        {
            //取資料 >> DTO
            List<VenueCardDto> venues = new CVenueFactory().QueryVenueCards();
            //回傳
            return Ok(ApiResult<List<VenueCardDto>>.Ok(venues));
        }


        //前台"場館資訊"頁 >> GET /api/venues/introduction
        //回傳開放時間 + 各運動類型的代表照片、注意事項、收費標準
        [HttpGet("introduction")]
        public IActionResult GetIntroduction()
        {
            //取得DTO
            SportTypeIntroPageDto page = new CSportTypeFactory().QueryIntroPage();

            //回傳DTO
            return Ok(ApiResult<SportTypeIntroPageDto>.Ok(page));
        }





    }
}
