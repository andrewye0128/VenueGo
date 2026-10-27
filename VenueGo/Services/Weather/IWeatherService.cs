using VenueGo.Dtos.WeatherDtos;

namespace VenueGo.Services.Weather
{
    /// <summary>首頁 TopBar 的即時氣象（10/18）。</summary>
    public interface IWeatherService
    {
        /// <summary>
        /// 現在天氣＋今天、明天、後天的預報。沒有設定授權碼時回 null（前台就不顯示天氣）。
        /// 一律用真實時間，不受時光機影響：天氣預報沒辦法回到過去或跳到未來。
        /// </summary>
        Task<WeatherDto?> GetAsync(CancellationToken cancellationToken = default);
    }
}
