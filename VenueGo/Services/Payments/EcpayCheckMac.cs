using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 綠界的防偽簽章 CheckMacValue：
    ///   送資料給綠界時要附上（證明是我們送的）；綠界送資料來時也會附上（我們要檢查，證明真的是綠界送的、內容沒被改過）
    ///
    /// 算法（綠界文件「檢查碼機制」）：
    ///   1. 拿掉 CheckMacValue 本身，其他欄位依名稱排序（不分大小寫，A→Z）
    ///   2. 組成 HashKey=…&amp;欄位1=值1&amp;欄位2=值2…&amp;HashIV=…
    ///   3. URL 編碼後整串轉小寫
    ///   4. SHA256，轉成大寫的 16 進位字串
    ///
    /// 用文件的範例資料驗證過，算出 6C51C9E6…5685B840，跟文件一致
    /// </summary>
    public static class EcpayCheckMac
    {
        public const string FieldName = "CheckMacValue";

        /// <summary>產生簽章：送資料給綠界之前呼叫</summary>
        public static string Create(IEnumerable<KeyValuePair<string, string>> fields, string hashKey, string hashIV)
        {
            var pairs = fields
                .Where(f => !string.Equals(f.Key, FieldName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
                .Select(f => $"{f.Key}={f.Value}");

            var raw = $"HashKey={hashKey}&{string.Join("&", pairs)}&HashIV={hashIV}";

            // HttpUtility.UrlEncode 的規則剛好跟綠界一樣：空白變成 +，- _ . ! * ( ) 不編碼
            // （不能用 WebUtility.UrlEncode 或 Uri.EscapeDataString，兩者的規則不一樣，算出來會不同）
            var encoded = HttpUtility.UrlEncode(raw).ToLowerInvariant();

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(encoded));
            return Convert.ToHexString(hash);   // 本來就是大寫
        }

        /// <summary>
        /// 檢查簽章：收到綠界送來的資料時呼叫。
        /// fields 要放綠界送來的「全部」欄位（包含我們沒用到的），少一個欄位算出來就不一樣
        /// </summary>
        public static bool Verify(IEnumerable<KeyValuePair<string, string>> fields, string hashKey, string hashIV)
        {
            var list = fields.ToList();

            var received = list
                .FirstOrDefault(f => string.Equals(f.Key, FieldName, StringComparison.OrdinalIgnoreCase))
                .Value;
            if (string.IsNullOrEmpty(received)) return false;

            var expected = Create(list, hashKey, hashIV);

            // 用「固定時間」的比對，不用 ==：== 遇到第一個不一樣的字就停，
            // 有心人可以從回應快慢一個字一個字猜出正確的簽章
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(received.ToUpperInvariant()));
        }
    }
}