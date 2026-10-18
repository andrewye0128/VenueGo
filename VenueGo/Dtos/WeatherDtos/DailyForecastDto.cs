namespace VenueGo.Dtos.WeatherDtos
{
    /// <summary>某一天的預報。Label 是「今天」「明天」「後天」；溫度是攝氏整數，拿不到就是 null。</summary>
    public sealed record DailyForecastDto(DateOnly Date, string Label, int? Min, int? Max, string? Weather);
}
