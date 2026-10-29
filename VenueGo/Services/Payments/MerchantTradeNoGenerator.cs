using System.Globalization;
using System.Security.Cryptography;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 綠界的交易編號 MerchantTradeNo：VG＋yyMMddHHmmss＋6 碼亂數，共 20 碼（綠界上限 20、只能英數字）。
    /// <para>
    /// 為什麼不用 PaymentId：測試帳號 3002607 全世界共用，而且重新執行初始化腳本後 PaymentId 會從頭算，
    /// 同一個號碼送兩次，綠界會拒絕（編號用過就不能再用）。
    /// </para>
    /// <para>
    /// 為什麼用 RandomNumberGenerator、不用 Random：Random 可以被推算出下一個值，
    /// RandomNumberGenerator 是密碼學等級的亂數，猜不到。
    /// </para>
    /// </summary>
    public static class MerchantTradeNoGenerator
    {
        public const int MaxLength = 20;

        private const string Prefix = "VG";
        private const int RandomLength = 6;

        // 只用大寫和數字：36 個字、6 碼約 21 億種組合，同一秒撞號的機率可以忽略
        private const string RandomChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        /// <param name="taipeiNow">真實的台灣時間（TimeProvider，不跟著時光機）</param>
        public static string Create(DateTimeOffset taipeiNow)
            => Prefix
               + taipeiNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)
               + RandomNumberGenerator.GetString(RandomChars, RandomLength);
    }
}