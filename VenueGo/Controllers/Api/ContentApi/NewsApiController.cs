using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.ContentDtos;
using VenueGo.Models.ContentModels;
using VenueGo.Services.Time;

namespace VenueGo.Controllers.Api.ContentApi
{
    [Route("api/news")]
    [ApiController]
    public class NewsApiController : ControllerBase
    {
        /*依賴注入*/
        private readonly ITimeService _timeService;

        public NewsApiController(ITimeService timeService)
        {
            _timeService = timeService;
        }


        //API >> 取得最新消息列表
        [HttpGet]
        public IActionResult GetNewsList(string? category, int page = 1, int pageSize = 9)
        {
            //呼叫Factory取得Dto
            CNewsFactory newsFactory = new CNewsFactory();
            PagedResult<NewsListItemDto> newsList = newsFactory.QueryPublicList(category, page,pageSize,_timeService.Now);

            //包成 ApiResult 格式回傳
            return Ok(ApiResult<PagedResult<NewsListItemDto>>.Ok(newsList));
        }


        //API >> 取得最新消息詳情
        [HttpGet("{id:int}")]
        public IActionResult GetNewsDetail(int id)
        {
            //呼叫Factory取得Dto
            CNewsFactory newsFactory = new CNewsFactory();
            NewsDetailDto? newsDetailDto = newsFactory.QueryPublicDetail(id, _timeService.Now);

            //檢查 newsDetail 的 null
            if(newsDetailDto == null)
            {
                return NotFound(ApiResult.Fail("找不到這則消息","NEWS_NOT_FOUND"));
            }

            //包成 ApiResult 格式回傳
            return Ok(ApiResult<NewsDetailDto>.Ok(newsDetailDto));
        }
    }
}
