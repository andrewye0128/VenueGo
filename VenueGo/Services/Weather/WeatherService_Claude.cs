using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VenueGo.Dtos.WeatherDtos;
using VenueGo.Models.Options;

namespace VenueGo.Services.Weather
{
    /// <summary>
    /// <see cref="IWeatherService"/> 的實作：向中央氣象署要資料、轉成 TopBar 要的格式、快取。
    /// <list type="bullet">
    ///   <item>授權碼放在 HTTP 標頭（Authorization），不放在網址上：HttpClient 的紀錄會印出完整網址，放網址上授權碼就會出現在主控台。</item>
    ///   <item>快取 CacheMinutes 分鐘：每個訪客打開首頁都會問一次，全部都轉去問氣象署的話，次數一下就用完了。</item>
    ///   <item>觀測和預報分開抓：一份失敗，另一份照樣回傳。兩份都失敗、而且之前抓成功過，就沿用上一次的（最多 6 小時）。</item>
    /// </list>
    /// </summary>
    public sealed class WeatherService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<CwaOptions> options,
        TimeProvider timeProvider,
        ILogger<WeatherService> logger) : IWeatherService
    {
        public const string HttpClientName = "Cwa";

        private const string CacheKey = "weather:v1";
        private const string LastGoodKey = "weather:v1:last-good";
        private static readonly TimeSpan LastGoodLifetime = TimeSpan.FromHours(6);

        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IMemoryCache _cache = cache;
        private readonly CwaOptions _options = options.Value;
        private readonly TimeProvider _timeProvider = timeProvider;
        private readonly ILogger<WeatherService> _logger = logger;

        /// <inheritdoc/>
        public async Task<WeatherDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured) return null;

            if (_cache.TryGetValue(CacheKey, out WeatherDto? cached) && cached is not null)
                return cached;

            // 真實時間（TaipeiTimeProvider），不是 ITimeService：時光機不影響天氣
            DateTimeOffset now = _timeProvider.GetLocalNow();

            var current = await FetchAsync(
                $"{_options.ObservationDataId}?StationId={Uri.EscapeDataString(_options.StationId)}",
                root => CwaWeatherParser.ParseObservation(root, _options.StationId),
                cancellationToken);

            var days = await FetchAsync(
                $"{_options.ForecastDataId}?LocationName={Uri.EscapeDataString(_options.LocationName)}",
                root => CwaWeatherParser.ParseForecast(root, _options.LocationName, now),
                cancellationToken);

            if (current is null && (days is null || days.Count == 0))
            {
                // 兩份都失敗：有上一次成功的就沿用（不放進短期快取，下一位訪客會再試一次）
                return _cache.TryGetValue(LastGoodKey, out WeatherDto? lastGood) ? lastGood : null;
            }

            var result = new WeatherDto(_options.DisplayName, current, days ?? []);
            _cache.Set(CacheKey, result, TimeSpan.FromMinutes(_options.CacheMinutes));
            _cache.Set(LastGoodKey, result, LastGoodLifetime);
            return result;
        }

        /// <summary>
        /// 抓一份資料並解析。網路錯誤、逾時、回應不是 JSON，一律記一筆警告、回 null。
        /// try/catch 的決定：這裡是真的「處理」錯誤的地方——天氣只是附加資訊，抓不到就少顯示一塊，不能讓首頁出錯。
        /// </summary>
        private async Task<T?> FetchAsync<T>(string relativeUrl, Func<JsonElement, T?> parse, CancellationToken cancellationToken)
            where T : class
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
                request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("氣象署回應 {StatusCode}：{Url}", (int)response.StatusCode, relativeUrl);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return parse(doc.RootElement);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "向氣象署要資料失敗：{Url}", relativeUrl);
                return null;
            }
        }
    }
}
