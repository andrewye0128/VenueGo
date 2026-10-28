using System.Globalization;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 綠界全方位金流（AIO）的固定規格：網址路徑、狀態代碼、日期格式。
    /// 集中在這裡，各個 Service 用同一份，不會一個地方改了、另一個地方漏掉。
    /// 依據官方文件：產生訂單 2862、付款結果通知 2878、查詢訂單 2890
    /// </summary>
    public static class EcpayProtocol
    {
        /// <summary>綠界所有日期時間欄位的格式（MerchantTradeDate、PaymentDate、TradeDate）</summary>
        public const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

        /// <summary>伺服器通知（ReturnURL）要回的文字：「收到了，不用再送」。大小寫、符號都不能錯</summary>
        public const string AckResponse = "1|OK";

        /// <summary>
        /// 我們給綠界的網址路徑。付款單（EcpayCheckoutService）和接收的 Controller 都用這裡的值，
        /// 兩邊才不會對不上
        /// </summary>
        public static class Routes
        {
            /// <summary>綠界伺服器通知（ReturnURL）：綠界的伺服器直接送來，要對外連得到（ngrok）</summary>
            public const string Return = "/api/payments/ecpay/return";

            /// <summary>瀏覽器帶回（OrderResultURL）：客人的瀏覽器送來，localhost 就收得到</summary>
            public const string Result = "/api/payments/ecpay/result";
        }

        /// <summary>付款結果通知的 RtnCode</summary>
        public static class RtnCodes
        {
            public const int Success = 1;

            /// <summary>付款結果待確認：不能當成功，也不能當失敗，要再查詢</summary>
            public const int PendingConfirmation = 10300066;
        }

        /// <summary>查詢訂單回答的 TradeStatus（綠界回的是字串）</summary>
        public static class TradeStatuses
        {
            /// <summary>訂單成立但還沒付款。實測：打開刷卡頁但沒刷卡，隔天還是 0，所以 0 不等於失敗</summary>
            public const string NotYetPaid = "0";

            public const string Paid = "1";

            /// <summary>交易失敗，客人沒有完成付款</summary>
            public const string Failed = "10200095";
        }

        /// <summary>DateTimeOffset → "2026/10/27 23:02:28"</summary>
        public static string FormatDateTime(DateTimeOffset value)
            // InvariantCulture：格式字串裡的 / 在某些語系會被換成別的符號，綠界只認 /
            => value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

        /// <summary>"2026/10/27 23:02:28" → DateTime；空字串或格式不對回 null（例如還沒付款時 PaymentDate 是空的）</summary>
        public static DateTime? ParseDateTime(string? value)
            => DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
                ? result
                : null;

        /// <summary>"1050" → 1050；空字串或不是數字回 null</summary>
        public static int? ParseInt(string? value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
    }
}