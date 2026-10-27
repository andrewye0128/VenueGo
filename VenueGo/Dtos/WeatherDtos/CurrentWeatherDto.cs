namespace VenueGo.Dtos.WeatherDtos
{
    /// <summary>現在天氣（觀測站實測）。Temperature 是攝氏、一位小數；Weather 是氣象署的文字（例如「多雲」），沒有就是 null。</summary>
    public sealed record CurrentWeatherDto(double Temperature, string? Weather, DateTimeOffset ObservedAt);
}
