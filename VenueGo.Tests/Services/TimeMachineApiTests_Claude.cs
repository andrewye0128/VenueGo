using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using VenueGo.Controllers;
using VenueGo.Dtos;
using VenueGo.Services;

namespace VenueGo.Tests.Services
{
    /// <summary>時光機 API（/api/dev/time）和欄位檢查的單元測試。</summary>
    public class TimeMachineApiTests
    {
        /// <summary>真實時間：台北 2026/10/7 22:42:00。</summary>
        private static readonly DateTime TaipeiStart = new(2026, 10, 7, 22, 42, 0);

        private static readonly TimeZoneInfo Taipei = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

        private readonly FakeTimeProvider _clock;

        public TimeMachineApiTests()
        {
            _clock = new FakeTimeProvider(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(TaipeiStart, Taipei)));
            _clock.SetLocalTimeZone(Taipei);
        }

        /// <summary>建立 API 和它用到的時光機、TimeService。存檔關掉（StateFile = ""）。</summary>
        private (TimeMachineApiController Sut, TimeMachine Machine, TimeService Time) Create(string environmentName = "Development")
        {
            var environment = Substitute.For<IWebHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [TimeMachine.StateFileKey] = "" })
                .Build();

            var machine = new TimeMachine(environment, configuration, _clock, NullLogger<TimeMachine>.Instance);
            var time = new TimeService(Substitute.For<IHttpClientFactory>(), NullLogger<TimeService>.Instance,
                                       new ConfigurationBuilder().Build(), _clock, machine);

            var fileVersion = Substitute.For<IFileVersionProvider>();
            fileVersion.AddFileVersionToPath(Arg.Any<PathString>(), Arg.Any<string>()).Returns(c => c.ArgAt<string>(1));

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost:7078");

            var sut = new TimeMachineApiController(environment, machine, time, _clock, fileVersion)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
            return (sut, machine, time);
        }

        private static TimeMachineTravelRequest Travel(int y, int mo, int d, int h = 0, int mi = 0, int s = 0)
            => new() { Year = y, Month = mo, Day = d, Hour = h, Minute = mi, Second = s };

        private static TimeMachineStateDto StateOf(IActionResult result)
            => result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<ApiResult<TimeMachineStateDto>>().Data!;

        // ════════════════════════════════════════════════════════
        //  欄位檢查（前端擋過一次，伺服器再擋一次）
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData(2026, 13, 1, 0, 0, 0, "月要在 1～12 之間")]
        [InlineData(2026, 0, 1, 0, 0, 0, "月要在 1～12 之間")]
        [InlineData(2026, 11, 31, 0, 0, 0, "2026 年 11 月只有 30 天")]
        [InlineData(2027, 2, 29, 0, 0, 0, "2027 年 2 月只有 28 天")]
        [InlineData(2026, 10, 0, 0, 0, 0, "2026 年 10 月只有 31 天")]
        [InlineData(2026, 10, 7, 24, 0, 0, "時要在 0～23 之間")]
        [InlineData(2026, 10, 7, 0, 60, 0, "分要在 0～59 之間")]
        [InlineData(2026, 10, 7, 0, 0, 60, "秒要在 0～59 之間")]
        [InlineData(1999, 10, 7, 0, 0, 0, "年要在 2000～2100 之間")]
        public void TryBuildTaipeiTime_超出範圍_回傳錯誤訊息(int y, int mo, int d, int h, int mi, int s, string expected)
        {
            bool ok = TimeMachineState.TryBuildTaipeiTime(Travel(y, mo, d, h, mi, s), out _, out string? error);

            ok.ShouldBeFalse();
            error.ShouldBe(expected);
        }

        [Theory]
        [InlineData(2028, 2, 29, 23, 59, 59)]   // 閏年
        [InlineData(2026, 1, 1, 0, 0, 0)]
        [InlineData(2026, 12, 31, 23, 59, 59)]
        public void TryBuildTaipeiTime_邊界上的合法時間_組得出來(int y, int mo, int d, int h, int mi, int s)
        {
            bool ok = TimeMachineState.TryBuildTaipeiTime(Travel(y, mo, d, h, mi, s), out DateTime result, out _);

            ok.ShouldBeTrue();
            result.ShouldBe(new DateTime(y, mo, d, h, mi, s));
        }

        [Theory]
        [InlineData(2026, 2026, new[] { 2025, 2026, 2027 })]
        [InlineData(2026, 2030, new[] { 2025, 2026, 2027, 2030 })]
        [InlineData(2026, 2025, new[] { 2025, 2026, 2027 })]
        public void YearOptions_去年今年明年_網站時間不在裡面就補上(int realYear, int siteYear, int[] expected)
        {
            TimeMachineState.YearOptions(realYear, siteYear).ShouldBe(expected);
        }

        [Theory]
        [InlineData(0, 0, 30, "不到 1 分鐘")]
        [InlineData(0, 1, 0, "1 小時")]
        [InlineData(23, 4, 0, "23 天 4 小時")]
        public void FormatSpan_天小時分(int days, int hours, int seconds, string expected)
        {
            TimeMachineState.FormatSpan(new TimeSpan(days, hours, 0, seconds)).ShouldBe(expected);
        }

        // ════════════════════════════════════════════════════════
        //  API
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Travel_時鐘有毫秒_網站時間剛好是指定的那一秒()
        {
            var (sut, _, time) = Create();
            _clock.Advance(TimeSpan.FromMilliseconds(789));

            var state = StateOf(sut.Travel(Travel(2026, 10, 30, 16, 45, 12)));

            time.Now.ShouldBe(new DateTime(2026, 10, 30, 16, 45, 12));
            state.SiteNow.ShouldBe("2026-10-30T16:45:12");
            state.Traveling.ShouldBeTrue();
        }

        [Fact]
        public void Travel_日期不合法_回400而且不旅行()
        {
            var (sut, machine, _) = Create();

            var result = sut.Travel(Travel(2026, 2, 30));

            result.ShouldBeOfType<BadRequestObjectResult>();
            machine.IsTraveling.ShouldBeFalse();
        }

        [Fact]
        public void Shift_負7天_網站時間往前7天()
        {
            var (sut, _, time) = Create();

            var state = StateOf(sut.Shift(new TimeMachineShiftRequest { Minutes = -7 * 24 * 60 }));

            time.Now.ShouldBe(TaipeiStart.AddDays(-7));
            state.OffsetText.ShouldBe("慢 7 天");
        }

        [Fact]
        public void Shift_0分鐘_回400()
        {
            var (sut, _, _) = Create();

            sut.Shift(new TimeMachineShiftRequest { Minutes = 0 }).ShouldBeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void Reset_旅行中_回到現在而且狀態寫著真實時間()
        {
            var (sut, machine, _) = Create();
            sut.Shift(new TimeMachineShiftRequest { Minutes = 1440 });

            var state = StateOf(sut.Reset(new TimeMachineResetRequest()));

            machine.IsTraveling.ShouldBeFalse();
            state.SiteNow.ShouldBe(state.RealNow);
            state.SiteNow.ShouldBe("2026-10-07T22:42:00");
        }

        [Fact]
        public void Get_旅行中_回傳真實時間和面板網址()
        {
            var (sut, _, _) = Create();
            sut.Shift(new TimeMachineShiftRequest { Minutes = 23 * 1440 + 4 * 60 });

            var state = StateOf(sut.Get());

            state.RealNow.ShouldBe("2026-10-07T22:42:00");
            state.SiteNow.ShouldBe("2026-10-31T02:42:00");
            state.OffsetText.ShouldBe("快 23 天 4 小時");
            state.PanelUrl.ShouldBe("https://localhost:7078/DevTools/Time");
            state.ScriptUrl.ShouldBe("https://localhost:7078/js/time-machine.js");
        }

        [Fact]
        public void 正式環境_每支API都回404()
        {
            var (sut, _, _) = Create(environmentName: "Production");

            sut.Get().ShouldBeOfType<NotFoundResult>();
            sut.Travel(Travel(2026, 10, 30)).ShouldBeOfType<NotFoundResult>();
            sut.Shift(new TimeMachineShiftRequest { Minutes = 60 }).ShouldBeOfType<NotFoundResult>();
            sut.Reset(new TimeMachineResetRequest()).ShouldBeOfType<NotFoundResult>();
        }
    }
}
