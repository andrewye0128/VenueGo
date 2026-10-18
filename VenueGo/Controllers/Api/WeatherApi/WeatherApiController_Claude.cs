using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.WeatherDtos;
using VenueGo.Services.Weather;

namespace VenueGo.Controllers.Api.WeatherApi
{
    // ════════════════════════════════════════════════════════════════
    //  首頁 TopBar 即時氣象（10/18）
    //  前台不直接呼叫氣象署：授權碼只能放在後端，而且要快取，不然每位訪客都會用掉一次額度。
    // ════════════════════════════════════════════════════════════════
    [ApiController]
    [Route("api/weather")]
    public sealed class WeatherApiController(IWeatherService weather) : ControllerBase
    {
        private readonly IWeatherService _weather = weather;

        /// <summary>即時氣象</summary>
        /// <remarks>
        /// 現在天氣（高雄站觀測）＋今天、明天、後天的預報（高雄市楠梓區），資料來源是中央氣象署開放資料。**不需要登入。**
        ///
        /// ### 注意
        /// - 後端快取 10 分鐘，同一段時間內每個人拿到的都一樣。
        /// - 一律用真實時間，**不受時光機影響**。
        /// - 沒有設定氣象署授權碼時，`data` 是 `null`（前台就不顯示天氣）。
        /// - 觀測或預報其中一份拿不到時，那一份是 `null`／空陣列，另一份照常回傳。
        /// </remarks>
        /// <response code="200">成功（data 可能是 null）</response>
        [HttpGet("")]
        // 屬性裡的型別不能寫 WeatherDto?（C# 不允許），文件上照樣標 WeatherDto；data 可能是 null 寫在 remarks
        [ProducesResponseType<ApiResult<WeatherDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(ApiResult<WeatherDto?>.Ok(await _weather.GetAsync(cancellationToken)));
    }
}
