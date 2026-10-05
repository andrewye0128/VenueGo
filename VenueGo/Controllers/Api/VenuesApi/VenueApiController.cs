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
        //前台「場館資訊」頁 >> GET /api/venues/introduction
        //回傳開放時間 + 各運動類型的代表照片、注意事項、收費標準
        //不需要登入(前台訪客也能看),所以不掛 EmployeeAuthorize
        //沒有任何運動類型時仍回傳 200,SportTypes 是空清單,由前台顯示「目前沒有場館資訊」
        [HttpGet("introduction")]
        public IActionResult GetIntroduction()
        {
            SportTypeIntroPageDto page = new CSportTypeFactory().QueryIntroPage();

            //用團隊統一的 ApiResult 包起來,前台 http.js 會自動取出 data
            return Ok(ApiResult<SportTypeIntroPageDto>.Ok(page));
        }
    }
}
