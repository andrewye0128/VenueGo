using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using VenueGo.Services;

namespace VenueGo.Tests.Services
{
    /// <summary>
    /// TimeService 的單元測試。
    /// <para>
    /// 不打真的時間 API、不看電腦的時鐘：
    /// 時間由 FakeTimeProvider 決定，API 的回應由 StubTimeApiHandler 決定，
    /// 所以每次跑的結果都一樣，也不需要網路。
    /// </para>
    /// </summary>
    public class TimeServiceTests
    {
        // ════════════════════════════════════════════════════════
        //  共用的準備
        // ════════════════════════════════════════════════════════

        /// <summary>測試裡的「現在」：台北 2026/10/6 09:00:00。</summary>
        private static readonly DateTime TaipeiStart = new(2026, 10, 6, 9, 0, 0);

        private static readonly TimeZoneInfo Taipei = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

        private readonly FakeTimeProvider _clock;
        private readonly StubTimeApiHandler _timeApi;
        private readonly TimeService _sut;

        /// <summary>
        /// xUnit 每個測試方法都會 new 一個新的測試類別，所以建構子就是「每個測試開始前」。
        /// 每個測試都拿到全新的時鐘和 TimeService，彼此不會互相影響。
        /// </summary>
        public TimeServiceTests()
        {
            // 時鐘：起始時間是台北 09:00，也就是 UTC 01:00。
            // ⚠️ FakeTimeProvider 的起始時間一定要給 UTC（時差 +00:00）。
            //    給 09:00+08:00 的話，它會把「09:00」直接當成 UTC，再加 8 小時變成 17:00。
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(TaipeiStart, Taipei);
            _clock = new FakeTimeProvider(new DateTimeOffset(utcStart));
            // 預設時區是 UTC，要改成台北，GetLocalNow() 才會回台北時間
            _clock.SetLocalTimeZone(Taipei);

            // 時間 API：每個測試自己決定它要回什麼
            _timeApi = new StubTimeApiHandler(_clock);

            // TimeService 跟 IHttpClientFactory 要 HttpClient 時，給它接上假 API 的那一個
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(TimeService.HttpClientName)
                             .Returns(new HttpClient(_timeApi));

            // 沒有設定任何值，TimeService 會用預設網址；反正請求不會真的送出去
            var configuration = new ConfigurationBuilder().Build();

            _sut = new TimeService(httpClientFactory, NullLogger<TimeService>.Instance, configuration, _clock);
        }

        /// <summary>CancellationToken 一律用 xUnit 提供的，測試被取消時才停得下來。</summary>
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        // ════════════════════════════════════════════════════════
        //  Now
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Now_尚未校時_回傳台北時間()
        {
            _sut.Now.ShouldBe(TaipeiStart);
        }

        [Fact]
        public void Now_時鐘有毫秒_捨去到秒()
        {
            _clock.Advance(TimeSpan.FromMilliseconds(789));

            _sut.Now.ShouldBe(TaipeiStart);
        }

        // ════════════════════════════════════════════════════════
        //  SyncAsync：成功
        // ════════════════════════════════════════════════════════

        [Fact]
        public async Task SyncAsync_API快一分鐘_Now也快一分鐘()
        {
            // Arrange
            _timeApi.RespondWith(TaipeiStart.AddMinutes(1));

            // Act
            await _sut.SyncAsync(Ct);

            // Assert
            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
            _sut.Now.ShouldBe(TaipeiStart.AddMinutes(1));
        }

        [Fact]
        public async Task SyncAsync_API慢一分鐘_Now也慢一分鐘()
        {
            _timeApi.RespondWith(TaipeiStart.AddMinutes(-1));

            await _sut.SyncAsync(Ct);

            _sut.Now.ShouldBe(TaipeiStart.AddMinutes(-1));
        }

        /// <summary>
        /// 死區：誤差不到 1 秒就當作 0。剛好 1 秒要採用。
        /// <para>邊界兩側各測一個，正負都測，「&lt;」寫成「&lt;=」就會被抓到。</para>
        /// </summary>
        [Theory]
        [InlineData(999, 0)]
        [InlineData(-999, 0)]
        [InlineData(1000, 1000)]
        [InlineData(-1000, -1000)]
        public async Task SyncAsync_誤差在死區邊界_小於一秒視為零(int apiAheadMs, int expectedOffsetMs)
        {
            _timeApi.RespondWith(TaipeiStart.AddMilliseconds(apiAheadMs));

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMilliseconds(expectedOffsetMs));
        }

        /// <summary>往返的時間要用「中點」來比，不能用收到回應的那一刻。</summary>
        [Fact]
        public async Task SyncAsync_往返一秒_用來回的中點計算誤差()
        {
            // 送出 09:00:00、收到 09:00:01，中點是 09:00:00.5。
            // API 說 09:01:00.5 → 誤差剛好 1 分鐘。
            // 如果程式誤用「收到的那一刻」，算出來會是 59.5 秒。
            _timeApi.RespondWith(TaipeiStart.AddMinutes(1).AddMilliseconds(500), roundTrip: TimeSpan.FromSeconds(1));

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task SyncAsync_往返剛好兩秒_仍然採用()
        {
            _timeApi.RespondWith(TaipeiStart.AddMinutes(1).AddSeconds(1), roundTrip: TimeSpan.FromSeconds(2));

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        // ════════════════════════════════════════════════════════
        //  SyncAsync：失敗時沿用上一次的偏移量
        //
        //  每個測試都先成功校時一次（快 1 分鐘），再讓第二次失敗。
        //  如果只測「失敗後偏移量是 0」，程式就算把偏移量歸零也會通過，
        //  測不出「沿用」這件事。
        // ════════════════════════════════════════════════════════

        /// <summary>先成功校時一次，讓偏移量變成 1 分鐘。</summary>
        private async Task SyncOneMinuteAheadAsync()
        {
            _timeApi.RespondWith(TaipeiStart.AddMinutes(1));
            await _sut.SyncAsync(Ct);
            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));   // 前提不成立就直接失敗，免得後面的斷言誤導人
        }

        [Fact]
        public async Task SyncAsync_往返超過兩秒_沿用上次的偏移量()
        {
            await SyncOneMinuteAheadAsync();
            _timeApi.RespondWith(TaipeiStart.AddMinutes(5), roundTrip: TimeSpan.FromSeconds(3));

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task SyncAsync_API回傳錯誤碼_沿用上次的偏移量(HttpStatusCode statusCode)
        {
            await SyncOneMinuteAheadAsync();
            _timeApi.RespondWithStatus(statusCode);

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("""{"dateTime":""}""")]
        [InlineData("""{"dateTime":"不是時間"}""")]
        [InlineData("這不是 JSON")]
        public async Task SyncAsync_回應內容不對_沿用上次的偏移量(string body)
        {
            await SyncOneMinuteAheadAsync();
            _timeApi.RespondWithBody(body);

            await _sut.SyncAsync(Ct);

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task SyncAsync_連不上API_不拋出例外並沿用上次的偏移量()
        {
            await SyncOneMinuteAheadAsync();
            _timeApi.FailWith(new HttpRequestException("模擬斷網"));

            // 背景服務每 30 分鐘呼叫一次，例外要在 SyncAsync 裡處理掉，不能往外丟
            await Should.NotThrowAsync(() => _sut.SyncAsync(Ct));

            _sut.Offset.ShouldBe(TimeSpan.FromMinutes(1));
        }

        // ════════════════════════════════════════════════════════
        //  假的時間 API
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 取代真正的網路。HttpClient 送出的請求最後都會進到 HttpMessageHandler，
        /// 這裡攔下來，直接回傳測試指定的內容。
        /// <para>
        /// 可以指定「往返花多久」：回應之前把假時鐘往前撥，
        /// TimeService 量到的「送出前」和「收到後」就會差這麼多。
        /// </para>
        /// </summary>
        private sealed class StubTimeApiHandler(FakeTimeProvider clock) : HttpMessageHandler
        {
            private Func<HttpResponseMessage> _respond = () => throw new InvalidOperationException("測試沒有設定 API 要回什麼");
            private TimeSpan _roundTrip = TimeSpan.Zero;

            /// <summary>API 回傳這個台北時間。</summary>
            public void RespondWith(DateTime apiTime, TimeSpan roundTrip = default)
            {
                // 格式照真正 API 的樣子：2026-09-21T11:07:15.2721376
                string json = $$"""{"dateTime":"{{apiTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}}"}""";
                RespondWithBody(json, roundTrip);
            }

            /// <summary>API 回傳 200，內容是這段文字。</summary>
            public void RespondWithBody(string body, TimeSpan roundTrip = default)
            {
                _roundTrip = roundTrip;
                _respond = () => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            }

            /// <summary>API 回傳錯誤狀態碼。</summary>
            public void RespondWithStatus(HttpStatusCode statusCode)
            {
                _roundTrip = TimeSpan.Zero;
                _respond = () => new HttpResponseMessage(statusCode);
            }

            /// <summary>連線本身失敗，例如斷網、DNS 查不到。</summary>
            public void FailWith(Exception exception)
            {
                _roundTrip = TimeSpan.Zero;
                _respond = () => throw exception;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                clock.Advance(_roundTrip);
                return Task.FromResult(_respond());
            }
        }
    }
}
