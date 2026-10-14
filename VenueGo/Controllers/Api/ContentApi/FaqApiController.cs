using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.ContentDtos;
using VenueGo.Models.ContentModels;

namespace VenueGo.Controllers.Api.ContentApi
{
    [Route("api/faqs")]
    [ApiController]
    public class FaqApiController : ControllerBase
    {
        //API >> 取得前台常見問題(一次回傳全部分類,前台切換頁籤不用再呼叫)
        //不用登入;分類順序、中文名稱由後端決定
        [HttpGet]
        public IActionResult GetFaqs()
        {
            List<FaqCategoryGroupDto> groups = new CFaqFactory().QueryPublicGroups();

            return Ok(ApiResult<List<FaqCategoryGroupDto>>.Ok(groups));
        }
    }
}
