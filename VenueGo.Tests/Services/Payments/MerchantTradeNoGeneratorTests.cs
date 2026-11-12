using Shouldly;
using VenueGo.Services.Payments;

namespace VenueGo.Tests.Services.Payments
{
    /// <summary>
    /// MerchantTradeNoGenerator（綠界交易編號：VG＋yyMMddHHmmss＋6 碼亂數）的單元測試。
    /// <para>
    /// 【為什麼要測】綠界規定交易編號最多 20 碼、只能英數字，不合規定綠界會拒絕付款單。
    /// </para>
    /// <para>
    /// 【亂數怎麼測】最後 6 碼每次都不一樣，不能寫死答案，所以只檢查「長得對不對」（格式），
    /// 不檢查「是哪幾個字」。
    /// </para>
    /// </summary>
    public class MerchantTradeNoGeneratorTests
    {
        // 晚上 21:05:03：順便確認時間是 24 小時制（寫錯成 12 小時制會變成 09）
        private static readonly DateTimeOffset TaipeiNow =
            new(2026, 11, 12, 21, 5, 3, TimeSpan.FromHours(8));

        [Fact]
        public void Create_長度_剛好是綠界上限20碼()
        {
            // 刻意寫 20，不用 MerchantTradeNoGenerator.MaxLength：
            // 20 是綠界的規定，如果有人把 MaxLength 改成別的數字，這個測試要能抓到
            MerchantTradeNoGenerator.Create(TaipeiNow).Length.ShouldBe(20);
        }

        [Fact]
        public void Create_前14碼_是VG加上24小時制的時間()
        {
            var tradeNo = MerchantTradeNoGenerator.Create(TaipeiNow);

            // [..14]：取前 14 個字
            tradeNo[..14].ShouldBe("VG261112210503");
        }

        [Fact]
        public void Create_後6碼_只有大寫英文和數字()
        {
            // 亂數可能剛好都是數字或都是英文，多產生幾個比較有把握
            for (var i = 0; i < 100; i++)
            {
                var tradeNo = MerchantTradeNoGenerator.Create(TaipeiNow);

                // [14..]：從第 15 個字取到最後
                // \z：真正的結尾（用 $ 的話，結尾多一個換行也會通過）
                tradeNo[14..].ShouldMatch(@"^[0-9A-Z]{6}\z");
            }
        }

        [Fact]
        public void Create_同一秒連續產生100個_都不一樣()
        {
            // 同一秒產生的編號，只能靠最後 6 碼的亂數區分。
            // 6 碼約 21 億種組合，100 個剛好撞號的機率約 40 萬分之一，可以忽略；
            // 如果有人把亂數改壞成固定值，這個測試會馬上失敗
            var tradeNos = Enumerable.Range(0, 100)
                .Select(_ => MerchantTradeNoGenerator.Create(TaipeiNow))
                .ToList();

            tradeNos.ShouldBeUnique();
        }
    }
}