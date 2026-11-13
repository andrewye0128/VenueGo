using System.Globalization;
using Shouldly;
using VenueGo.Services.Payments;

namespace VenueGo.Tests.Services.Payments
{
    /// <summary>
    /// EcpayProtocol（綠界的日期、數字格式）的單元測試。
    /// <para>
    /// 【為什麼要測】綠界只認 "2026/11/12 21:05:03" 這種格式，格式錯了綠界會拒絕付款單；
    /// 綠界送回來的文字也要能安全地轉回日期、數字，不對的時候回 null，不能讓程式當掉。
    /// </para>
    /// </summary>
    public class EcpayProtocolTests
    {
        private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

        // ════════════════════════════════════════════════════════
        //  FormatDateTime：日期 → 給綠界的文字
        // ════════════════════════════════════════════════════════

        [Fact]
        public void FormatDateTime_個位數的月日時_前面補零()
        {
            var time = new DateTimeOffset(2026, 1, 5, 9, 5, 3, TaipeiOffset);

            EcpayProtocol.FormatDateTime(time).ShouldBe("2026/01/05 09:05:03");
        }

        [Fact]
        public void FormatDateTime_晚上的時間_用24小時制()
        {
            // HH 是 24 小時制、hh 是 12 小時制：寫錯成 hh 會變成 09:05:03，跟早上分不出來
            var time = new DateTimeOffset(2026, 11, 12, 21, 5, 3, TaipeiOffset);

            EcpayProtocol.FormatDateTime(time).ShouldBe("2026/11/12 21:05:03");
        }

        [Fact]
        public void FormatDateTime_電腦語系是德文_還是用斜線()
        {
            // 格式字串裡的 / 在 .NET 代表「這個語系的日期分隔符號」，德文是「.」。
            // FormatDateTime 有加 InvariantCulture 才會固定是 /。
            // 在台灣的電腦上，有沒有加結果都一樣，所以故意切換成德文來測
            var time = new DateTimeOffset(2026, 11, 12, 21, 5, 3, TaipeiOffset);
            var originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                // 先確認：德文語系下，沒加 InvariantCulture 真的會變成「.」。
                // 如果這台電腦沒有德文語系的資料，這行會失敗，提醒我們這個測試沒有在測東西
                time.ToString(EcpayProtocol.DateTimeFormat).ShouldBe("2026.11.12 21:05:03");

                EcpayProtocol.FormatDateTime(time).ShouldBe("2026/11/12 21:05:03");
            }
            finally
            {
                // 測試失敗也一定會執行：把語系改回來，不影響其他測試
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        // ════════════════════════════════════════════════════════
        //  ParseDateTime：綠界送來的文字 → 日期
        // ════════════════════════════════════════════════════════

        [Fact]
        public void ParseDateTime_正確的格式_轉成日期時間()
        {
            EcpayProtocol.ParseDateTime("2026/10/27 23:02:28")
                .ShouldBe(new DateTime(2026, 10, 27, 23, 2, 28));
        }

        [Theory]
        [InlineData("2026-10-27 23:02:28")]   // 用 - 分隔
        [InlineData("2026/1/5 9:05:03")]      // 沒有補零
        [InlineData("2026/13/01 00:00:00")]   // 沒有 13 月
        [InlineData("")]                      // 還沒付款時，綠界送來的 PaymentDate 是空的
        [InlineData(null)]                    // 綠界沒有送這個欄位
        public void ParseDateTime_格式不對或沒有值_回傳Null(string? value)
        {
            EcpayProtocol.ParseDateTime(value).ShouldBeNull();
        }

        // ════════════════════════════════════════════════════════
        //  ParseInt：綠界送來的文字 → 整數
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData("1050", 1050)]
        [InlineData("0", 0)]
        [InlineData("-5", -5)]
        [InlineData("abc", null)]             // 不是數字
        [InlineData("12.5", null)]            // 有小數點
        [InlineData("1,050", null)]           // 有千分位逗號
        [InlineData("99999999999", null)]     // 超過 int 的上限（約 21 億）
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseInt_各種輸入_回傳數字或Null(string? value, int? expected)
        {
            EcpayProtocol.ParseInt(value).ShouldBe(expected);
        }
    }
}