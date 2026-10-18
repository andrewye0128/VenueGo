
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using VenueGo.Enums;
using VenueGo.Services.Ticket;
using VenueGo.Services.Time;

namespace VenueGo.Tests.Services
{
    /// <summary>
    /// QrTokenService 的單元測試。
    /// 時間由假的 ITimeService 決定，可以任意撥時間；
    /// 只測不碰資料庫的部分（Generate / TryParse / Verify），所以 CEntryTicketFactory 傳 null 就好。
    /// </summary>
    public class QrTokenServiceTests
    {
        // 剛好是某一格時間窗的開頭（14:00:30 能被 30 秒整除），這樣「+59 秒還有效、+60 秒過期」的邊界才清楚
        private static readonly DateTime WindowStart = new(2026, 10, 18, 14, 0, 30);

        private const int TicketId = 42;
        private const string Qrtoken = "TCK-test-qrtoken-0001";

        private readonly ITimeService _time;
        private readonly QrTokenService _sut;

        public QrTokenServiceTests()
        {
            _time = Substitute.For<ITimeService>();
            _time.Now.Returns(WindowStart);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["QrSigning:Key"] = "unit-test-signing-key-at-least-32-chars-long"
                })
                .Build();

            _sut = new QrTokenService(config, _time, null!);
        }

        /// <summary>
        /// 模擬 VerifyAsync 的流程，只是 Qrtoken 由測試直接給，不去資料庫撈。
        /// </summary>
        private QrVerifyResult VerifyPayload(string payload, string qrtoken)
        {
            if (!_sut.TryParse(payload, out int ticketId, out long window, out string signature))
                return QrVerifyResult.Fail(QrVerifyFailReason.InvalidFormat);

            return _sut.Verify(ticketId, window, signature, qrtoken);
        }

        // ════════════════════════════════════════════════════════
        //  正常情況
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Generate後馬上驗證_通過並帶出票號()
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);

            QrVerifyResult result = VerifyPayload(payload, Qrtoken);

            result.Success.ShouldBeTrue();
            result.TicketId.ShouldBe(TicketId);
            result.Reason.ShouldBeNull();
        }

        [Fact]
        public void Generate_格式是v1_票號_時間窗_簽章()
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);

            string[] parts = payload.Split('.');
            parts.Length.ShouldBe(4);
            parts[0].ShouldBe("v1");
            parts[1].ShouldBe("42");
        }

        [Fact]
        public void Generate_到期時間是這一格的結束_也就是30秒後()
        {
            var (_, expiresAt) = _sut.Generate(TicketId, Qrtoken);

            expiresAt.ShouldBe(WindowStart.AddSeconds(30));
        }

        [Fact]
        public void 同一格時間窗內產生兩次_內容相同()
        {
            var (first, _) = _sut.Generate(TicketId, Qrtoken);
            _time.Now.Returns(WindowStart.AddSeconds(29));
            var (second, _) = _sut.Generate(TicketId, Qrtoken);

            second.ShouldBe(first);
        }

        [Fact]
        public void 跨到下一格時間窗_內容不同()
        {
            var (first, _) = _sut.Generate(TicketId, Qrtoken);
            _time.Now.Returns(WindowStart.AddSeconds(30));
            var (second, _) = _sut.Generate(TicketId, Qrtoken);

            second.ShouldNotBe(first);
        }

        // ════════════════════════════════════════════════════════
        //  時間窗：現在這格和前一格有效，其他都過期
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData(0)]    // 同一格
        [InlineData(29)]   // 同一格的最後一秒
        [InlineData(30)]   // 下一格開頭：QR 是前一格的，仍然有效
        [InlineData(59)]   // 下一格的最後一秒：仍然有效
        public void 產生後59秒內_仍然有效(int secondsLater)
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);
            _time.Now.Returns(WindowStart.AddSeconds(secondsLater));

            VerifyPayload(payload, Qrtoken).Success.ShouldBeTrue();
        }

        [Theory]
        [InlineData(60)]    // 剛好隔兩格
        [InlineData(300)]
        public void 產生後60秒以上_已過期(int secondsLater)
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);
            _time.Now.Returns(WindowStart.AddSeconds(secondsLater));

            QrVerifyResult result = VerifyPayload(payload, Qrtoken);

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.Expired);
        }

        [Fact]
        public void QR的時間窗比現在還新_視為過期()
        {
            // 在 14:00:30 產生，卻拿到 14:00:00（上一格）去驗證，代表時鐘對不上
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);
            _time.Now.Returns(WindowStart.AddSeconds(-30));

            QrVerifyResult result = VerifyPayload(payload, Qrtoken);

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.Expired);
        }

        // ════════════════════════════════════════════════════════
        //  簽章：被竄改、Qrtoken 被換掉都要失敗
        // ════════════════════════════════════════════════════════

        [Fact]
        public void 簽章被改一個字元_驗證失敗()
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);
            string[] parts = payload.Split('.');
            parts[3] = (parts[3][0] == 'A' ? 'B' : 'A') + parts[3][1..];

            QrVerifyResult result = VerifyPayload(string.Join('.', parts), Qrtoken);

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.InvalidSignature);
        }

        [Fact]
        public void 票號被改成別張票_驗證失敗()
        {
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);
            string[] parts = payload.Split('.');
            parts[1] = "43";

            QrVerifyResult result = VerifyPayload(string.Join('.', parts), Qrtoken);

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.InvalidSignature);
        }
        [Fact]
        public void 票券的Qrtoken被換掉_舊QR失敗()
        {
            // 補發或轉讓會換 Qrtoken：舊 QR 是用舊的算的，拿新的去驗就對不上
            var (payload, _) = _sut.Generate(TicketId, Qrtoken);

            QrVerifyResult result = VerifyPayload(payload, "TCK-test-qrtoken-NEW");

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.InvalidSignature);
        }

        // ════════════════════════════════════════════════════════
        //  格式：亂字串都要擋在第一關
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("TCK-亂寫的舊格式")]
        [InlineData("v1.42.100")]            // 少一段
        [InlineData("v1.42.100.sig.extra")]  // 多一段
        [InlineData("v2.42.100.sig")]        // 版本不是 v1
        [InlineData("v1.abc.100.sig")]       // 票號不是數字
        [InlineData("v1.0.100.sig")]         // 票號不是正數
        [InlineData("v1.42.abc.sig")]        // 時間窗不是數字
        [InlineData("v1.42.100.")]           // 簽章是空的
        public void 格式不對_TryParse回false(string? payload)
        {
            _sut.TryParse(payload, out _, out _, out _).ShouldBeFalse();
        }

        [Fact]
        public void 格式正確_TryParse拆出三個值()
        {
            bool ok = _sut.TryParse("v1.42.100.abcSig", out int ticketId, out long window, out string signature);

            ok.ShouldBeTrue();
            ticketId.ShouldBe(42);
            window.ShouldBe(100);
            signature.ShouldBe("abcSig");
        }

        [Fact]
        public async Task VerifyAsync_格式錯誤時_不碰資料庫就回InvalidFormat()
        {
            // 建構時 CEntryTicketFactory 傳的是 null；如果程式去撈票，這裡會直接炸
            QrVerifyResult result = await _sut.VerifyAsync("亂寫的");

            result.Success.ShouldBeFalse();
            result.Reason.ShouldBe(QrVerifyFailReason.InvalidFormat);
        }
    }
}