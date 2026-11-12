using Shouldly;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Tests.Models.ReservationModels
{
    /// <summary>
    /// InvoiceRules（手機條碼載具）的單元測試。
    /// <para>
    /// 【為什麼要測】後台代客預約、前台會員預約都用這一份規則，改壞了兩邊一起壞。
    /// 前端 src/constants/payment.js 的 CARRIER_NO_PATTERN 也要跟這裡一致。
    /// </para>
    /// </summary>
    public class InvoiceRulesTests
    {
        // ════════════════════════════════════════════════════════
        //  IsValidCarrierNo：格式對不對
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData("/AB12345")]
        [InlineData("/A+-.123")]   // + - . 三個符號也可以
        [InlineData("/1234567")]   // 全部數字
        [InlineData("/ABCDEFG")]   // 全部英文
        public void IsValidCarrierNo_格式正確_回傳True(string carrierNo)
        {
            InvoiceRules.IsValidCarrierNo(carrierNo).ShouldBeTrue();
        }

        [Theory]
        [InlineData("AB123456")]     // 沒有斜線
        [InlineData("/AB1234")]      // 只有 6 碼
        [InlineData("/AB123456")]    // 有 8 碼
        [InlineData("/ab12345")]     // 小寫：IsValidCarrierNo 不會幫忙轉大寫，要先呼叫 NormalizeCarrierNo
        [InlineData("/AB 2345")]     // 中間有空白
        [InlineData(" /AB12345")]    // 前面有空白：同上，要先 Normalize
        [InlineData("/AB12345\n")]   // 結尾多一個換行（格式用 \z 才擋得下來，用 $ 會通過）
        [InlineData("")]
        [InlineData(null)]
        public void IsValidCarrierNo_格式錯誤_回傳False(string? carrierNo)
        {
            InvoiceRules.IsValidCarrierNo(carrierNo).ShouldBeFalse();
        }

        // ════════════════════════════════════════════════════════
        //  NormalizeCarrierNo：整理客人輸入的號碼
        // ════════════════════════════════════════════════════════

        [Theory]
        [InlineData(" /ab12345 ", "/AB12345")]   // 去掉前後空白、轉大寫
        [InlineData("/AB12345", "/AB12345")]     // 本來就正確：不變
        [InlineData("   ", null)]                // 只有空白：當作沒填
        [InlineData("", null)]
        [InlineData(null, null)]
        public void NormalizeCarrierNo_各種輸入_回傳整理後的號碼(string? input, string? expected)
        {
            InvoiceRules.NormalizeCarrierNo(input).ShouldBe(expected);
        }
    }
}