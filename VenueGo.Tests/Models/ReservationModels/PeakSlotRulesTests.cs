using System.Globalization;
using Shouldly;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Tests.Models.ReservationModels
{
    /// <summary>
    /// PeakSlotRules（訂單明細的尖峰／離峰標籤）的單元測試。
    /// <para>
    /// 【為什麼要測】規則有兩條路：新訂單用下單時存下來的值，舊訂單（IsPeak 是 null）才用時間推算。
    /// 改程式時很容易把順序弄反，讓舊訂單的標籤跟著現在的尖峰設定變動。
    /// </para>
    /// </summary>
    public class PeakSlotRulesTests
    {
        // ════════════════════════════════════════════════════════
        //  IsPeak：這一格是不是尖峰
        //  時間用字串寫：[InlineData] 只能放常數，TimeOnly 不行，所以先寫 "18:00"，再用 ToTime 轉
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData(true, "08:00", "18:00", true)]    // 推算會是離峰，但存下來的是尖峰 → 尖峰
        [InlineData(false, "19:00", "18:00", false)]  // 推算會是尖峰，但存下來的是離峰 → 離峰
        public void IsPeak_有存下來的值_用存下來的值(
            bool storedIsPeak, string slotTime, string peakStartTime, bool expected)
        {
            PeakSlotRules.IsPeak(storedIsPeak, ToTime(slotTime), ToTime(peakStartTime))
                .ShouldBe(expected);
        }

        [Theory]
        [InlineData("18:00", "18:00", true)]   // 剛好等於尖峰起始時間：算尖峰（>=）
        [InlineData("17:00", "18:00", false)]
        [InlineData("19:00", "18:00", true)]
        [InlineData(null, "18:00", false)]     // 舊資料沒有這格的時間：當作離峰
        [InlineData("19:00", null, false)]     // 那天不分尖峰／離峰：當作離峰
        public void IsPeak_沒有存下來的值_用時間推算(
            string? slotTime, string? peakStartTime, bool expected)
        {
            PeakSlotRules.IsPeak(null, ToTime(slotTime), ToTime(peakStartTime))
                .ShouldBe(expected);
        }

        /// <summary>"18:00" → TimeOnly；null 還是 null</summary>
        private static TimeOnly? ToTime(string? value)
            => value is null ? null : TimeOnly.Parse(value, CultureInfo.InvariantCulture);

        // ════════════════════════════════════════════════════════
        //  NeedsFallback：要不要向場地模組查尖峰起始時間
        //  參數是 bool? 的清單，一樣不能放進 [InlineData]，情況只有 3 種，寫成 3 個 [Fact] 最好讀
        // ════════════════════════════════════════════════════════

        [Fact]
        public void NeedsFallback_沒有明細_回傳False()
        {
            PeakSlotRules.NeedsFallback(Array.Empty<bool?>()).ShouldBeFalse();
        }

        [Fact]
        public void NeedsFallback_每一格都有存下來的值_回傳False()
        {
            PeakSlotRules.NeedsFallback(new bool?[] { true, false }).ShouldBeFalse();
        }

        [Fact]
        public void NeedsFallback_有一格是Null_回傳True()
        {
            PeakSlotRules.NeedsFallback(new bool?[] { true, null, false }).ShouldBeTrue();
        }
    }
}