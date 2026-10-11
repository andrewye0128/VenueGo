using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using VenueGo.Services;

namespace VenueGo.Tests.Services
{
    /// <summary>
    /// TimeMachine 與 TimeService 搭配時光機的單元測試。
    /// <para>
    /// 時光機會把狀態存檔（重新啟動後還記得）。每個測試都用自己的暫存檔，
    /// 測完刪掉，不會碰到你電腦上真正的 %LOCALAPPDATA%\VenueGo\time-machine.json。
    /// </para>
    /// </summary>
    public class TimeMachineTests : IDisposable
    {
        /// <summary>真實時間：台北 2026/10/6 09:00:00。</summary>
        private static readonly DateTime TaipeiStart = new(2026, 10, 6, 9, 0, 0);

        private static readonly TimeZoneInfo Taipei = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

        private readonly FakeTimeProvider _clock;

        /// <summary>這個測試專用的存檔位置（每個測試一個資料夾）。</summary>
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "VenueGoTests", Guid.NewGuid().ToString("N"));
        private string StateFile => Path.Combine(_folder, "time-machine.json");

        public TimeMachineTests()
        {
            _clock = new FakeTimeProvider(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(TaipeiStart, Taipei)));
            _clock.SetLocalTimeZone(Taipei);
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        }

        /// <summary>
        /// 建立時光機。environmentName 預設是開發環境；travelTo 是設定檔裡的值。
        /// stateFile 不給就用這個測試專用的暫存檔；給空字串代表不存檔。
        /// 同一個測試呼叫兩次＝模擬「網站重新啟動」。
        /// </summary>
        private TimeMachine CreateTimeMachine(string environmentName = "Development", string? travelTo = null, string? stateFile = null)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);

            var settings = new Dictionary<string, string?> { [TimeMachine.StateFileKey] = stateFile ?? StateFile };
            if (travelTo != null) settings[TimeMachine.TravelToKey] = travelTo;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            return new TimeMachine(environment, configuration, _clock, NullLogger<TimeMachine>.Instance);
        }

        /// <summary>建立搭配時光機的 TimeService。這幾個測試不會校時，所以 HttpClientFactory 給空的就好。</summary>
        private TimeService CreateTimeService(ITimeMachine? timeMachine)
            => new(Substitute.For<IHttpClientFactory>(), NullLogger<TimeService>.Instance,
                   new ConfigurationBuilder().Build(), _clock, timeMachine);

        // ════════════════════════════════════════════════════════
        //  TimeMachine 本身
        // ════════════════════════════════════════════════════════

        [Fact]
        public void 建立_設定檔沒寫_不旅行()
        {
            var sut = CreateTimeMachine();

            sut.IsTraveling.ShouldBeFalse();
            sut.TravelOffset.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public void 建立_設定檔寫了目的地_啟動就出發()
        {
            var sut = CreateTimeMachine(travelTo: "2026-10-30 16:45");

            sut.TravelOffset.ShouldBe(new DateTime(2026, 10, 30, 16, 45, 0) - TaipeiStart);
        }

        [Theory]
        [InlineData("2026-10-30 16:45")]
        [InlineData("2026-10-30T16:45")]
        [InlineData("2026/10/30 16:45")]
        public void 建立_各種日期寫法_都看得懂(string travelTo)
        {
            var sut = CreateTimeMachine(travelTo: travelTo);

            sut.TravelOffset.ShouldBe(new DateTime(2026, 10, 30, 16, 45, 0) - TaipeiStart);
        }

        [Theory]
        [InlineData("下個月")]
        [InlineData("30/10/2026")]
        public void 建立_日期看不懂_不旅行也不拋出例外(string travelTo)
        {
            var sut = CreateTimeMachine(travelTo: travelTo);

            sut.IsTraveling.ShouldBeFalse();
        }

        [Fact]
        public void 建立_正式環境就算有設定_也不旅行()
        {
            var sut = CreateTimeMachine(environmentName: "Production", travelTo: "2026-10-30 16:45");

            sut.IsAvailable.ShouldBeFalse();
            sut.IsTraveling.ShouldBeFalse();
        }

        [Fact]
        public void TravelTo_正式環境_拋出例外()
        {
            var sut = CreateTimeMachine(environmentName: "Production");

            Should.Throw<InvalidOperationException>(() => sut.TravelTo(TaipeiStart.AddDays(1)));
        }

        [Fact]
        public void TravelBy_連續兩次_偏移量累加()
        {
            var sut = CreateTimeMachine();

            sut.TravelBy(TimeSpan.FromDays(1));
            sut.TravelBy(TimeSpan.FromHours(-2));

            sut.TravelOffset.ShouldBe(TimeSpan.FromHours(22));
        }

        [Fact]
        public void Reset_旅行中_回到真實時間()
        {
            var sut = CreateTimeMachine(travelTo: "2026-10-30 16:45");

            sut.Reset();

            sut.IsTraveling.ShouldBeFalse();
        }

        [Fact]
        public void Stamp_每調整一次就變一次()
        {
            var sut = CreateTimeMachine();
            sut.Stamp.ShouldBe(0);

            sut.TravelBy(TimeSpan.FromDays(1));
            long first = sut.Stamp;
            sut.TravelBy(TimeSpan.FromDays(1));   // 時鐘沒動，同一個 Tick 內調整兩次

            first.ShouldNotBe(0);
            sut.Stamp.ShouldNotBe(first);
        }

        // ════════════════════════════════════════════════════════
        //  存檔：網站重新啟動後還記得
        //  「同一個測試裡建立兩次時光機」就是在模擬重新啟動。
        // ════════════════════════════════════════════════════════

        [Fact]
        public void 重新啟動_旅行中_還原上次的偏移量()
        {
            var before = CreateTimeMachine();
            before.TravelBy(TimeSpan.FromDays(3));

            var after = CreateTimeMachine();

            after.TravelOffset.ShouldBe(TimeSpan.FromDays(3));
            after.Stamp.ShouldBe(before.Stamp);
        }

        [Fact]
        public void 重新啟動_按過回到現在_設定檔的目的地不會又套用()
        {
            var before = CreateTimeMachine(travelTo: "2026-10-30 16:45");
            before.Reset();

            var after = CreateTimeMachine(travelTo: "2026-10-30 16:45");

            after.IsTraveling.ShouldBeFalse();
        }

        [Fact]
        public void 重新啟動_設定檔的目的地改過_以設定檔為準()
        {
            var before = CreateTimeMachine(travelTo: "2026-10-30 16:45");
            before.TravelBy(TimeSpan.FromDays(5));

            var after = CreateTimeMachine(travelTo: "2026-12-01 10:00");

            after.TravelOffset.ShouldBe(new DateTime(2026, 12, 1, 10, 0, 0) - TaipeiStart);
        }

        [Fact]
        public void 重新啟動_存檔內容壞掉_當作沒存過也不拋出例外()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(StateFile, "這不是 JSON");

            var sut = CreateTimeMachine(travelTo: "2026-10-30 16:45");

            sut.TravelOffset.ShouldBe(new DateTime(2026, 10, 30, 16, 45, 0) - TaipeiStart);
        }

        [Fact]
        public void 存檔位置是空字串_不存檔()
        {
            var sut = CreateTimeMachine(stateFile: "");

            sut.TravelBy(TimeSpan.FromDays(1));

            Directory.Exists(_folder).ShouldBeFalse();
        }

        [Fact]
        public void 正式環境_不讀也不寫存檔()
        {
            var dev = CreateTimeMachine();
            dev.TravelBy(TimeSpan.FromDays(1));

            var production = CreateTimeMachine(environmentName: "Production");

            production.IsTraveling.ShouldBeFalse();
        }

        // ════════════════════════════════════════════════════════
        //  TimeService 搭配時光機
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Now_跳到指定時間_回傳那個時間()
        {
            var timeMachine = CreateTimeMachine();
            var sut = CreateTimeService(timeMachine);

            timeMachine.TravelTo(new DateTime(2026, 10, 30, 16, 45, 0));

            sut.Now.ShouldBe(new DateTime(2026, 10, 30, 16, 45, 0));
        }

        [Fact]
        public void Now_出發之後時間照常往前走()
        {
            var timeMachine = CreateTimeMachine(travelTo: "2026-10-30 16:45");
            var sut = CreateTimeService(timeMachine);

            _clock.Advance(TimeSpan.FromMinutes(10));

            sut.Now.ShouldBe(new DateTime(2026, 10, 30, 16, 55, 0));
        }

        [Fact]
        public void Now_沒有時光機_就是真實時間()
        {
            var sut = CreateTimeService(timeMachine: null);

            sut.Now.ShouldBe(TaipeiStart);
        }
    }
}
