namespace VenueGo.Dtos.WeatherDtos
{
    /// <summary>
    /// 首頁 TopBar 的即時氣象（10/18）。
    /// Current、Days 可能是 null／空的：氣象署那一份資料拿不到時，另一份照樣回傳，前台各自處理。
    /// </summary>
    public sealed record WeatherDto(
        string PlaceName,
        CurrentWeatherDto? Current,
        IReadOnlyList<DailyForecastDto> Days);
}
