namespace VenueGo.Dtos.WeatherDtos
{
    /// <summary>某一天的預報。Label 是「今天」「明天」「後天」；溫度是攝氏整數，拿不到就是 null。</summary>
    /// <param name="RainChance">10/25 新增：降雨機率（0～100 的整數，%）。取那一天各段 12 小時降雨機率的最高值；拿不到就是 null。</param>
    public sealed record DailyForecastDto(DateOnly Date, string Label, int? Min, int? Max, string? Weather, int? RainChance);
}
