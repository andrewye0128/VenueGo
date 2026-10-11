using System;
using System.Collections.Generic;
using System.Text;
using Shouldly;
using VenueGo.Services.CheckIn;

namespace VenueGo.Tests.Services
{
    public class OvertimeFeeTests
    {
        // 預約結束時間固定一個，每個測試只決定「超時幾秒」
        private static readonly DateTime EndTime = new(2026, 10, 7, 18, 0, 0);

        // 邊界案例：超時秒數 → 預期分鐘、計費小時、金額
        // (小數不能直接放進 InlineData，所以計費小時用 double 傳，比較時再轉成 decimal)
        [Theory]
        [InlineData(-600, 0, 0.0, 0)]      // 提早 10 分鐘離場 → 沒超時
        [InlineData(0, 0, 0.0, 0)]         // 準時離場
        [InlineData(300, 5, 0.0, 0)]       // 5 分 0 秒 → 免費
        [InlineData(359, 5, 0.0, 0)]       // 5 分 59 秒 → 捨去成 5 分鐘 → 免費
        [InlineData(360, 6, 0.5, 250)]     // 6 分 0 秒 → 開始收 0.5 小時
        [InlineData(1799, 29, 0.5, 250)]   // 29 分 59 秒 → 捨去成 29 分鐘 → 還是 0.5 小時
        [InlineData(1800, 30, 0.5, 250)]   // 30 分 0 秒 → 收 0.5 小時
        [InlineData(3600, 60, 1.0, 500)]   // 剛好 1 小時
        [InlineData(4500, 75, 1.0, 500)]   // 75 分鐘 → 封頂，不再往上疊
        public void Calculate_依超時秒數_算出分鐘與金額(int overdueSeconds, int expectedMinutes, double expectedHours, int expectedAmount)
        {
            DateTime actualOutTime = EndTime.AddSeconds(overdueSeconds);

            OvertimeFeeResult result = OvertimeFee.Calculate(EndTime, actualOutTime);

            result.OverdueMinutes.ShouldBe(expectedMinutes);
            result.BillingHours.ShouldBe((decimal)expectedHours);
            result.ChargeAmount.ShouldBe(expectedAmount);
        }
        [Fact]
        public void Calculate_任何情況_單價都是500()
        {
            OvertimeFeeResult result = OvertimeFee.Calculate(EndTime, EndTime.AddMinutes(10));

            result.UnitPrice.ShouldBe(500);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(359, true)]    // 5 分 59 秒
        [InlineData(360, false)]   // 6 分 0 秒
        [InlineData(4500, false)]
        public void IsFree_免費才是true(int overdueSeconds, bool expectedIsFree)
        {
            OvertimeFeeResult result = OvertimeFee.Calculate(EndTime, EndTime.AddSeconds(overdueSeconds));

            result.IsFree.ShouldBe(expectedIsFree);
        }
    }
}
