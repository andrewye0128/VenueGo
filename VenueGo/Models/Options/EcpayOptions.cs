namespace VenueGo.Models.Options
{
    /// <summary>
    /// 綠界金流（全方位金流 AIO）的設定，對應設定檔的 Ecpay 節點。
    /// 金鑰放在 appsettings.Local.json（不進 git），範本在 appsettings.Local.example.json。
    ///
    ///   "Ecpay": {
    ///     "MerchantId": "3002607",
    ///     "HashKey": "…",
    ///     "HashIV": "…",
    ///     "NotifyBaseUrl": "https://你的固定網址.ngrok-free.app"
    ///   }
    ///
    /// 網址的預設值都是綠界的「測試環境」；正式上線時才在設定檔改成正式網址。
    /// </summary>
    public sealed class EcpayOptions
    {
        public const string SectionName = "Ecpay";

        /// <summary>特店編號（綠界給的）</summary>
        public string MerchantId { get; set; } = string.Empty;

        /// <summary>產生 CheckMacValue 用的金鑰。只放在設定檔，不寫進資料庫、不寫進 log</summary>
        public string HashKey { get; set; } = string.Empty;
        public string HashIV { get; set; } = string.Empty;

        /// <summary>付款頁：前端把表單送到這裡，畫面就跳到綠界</summary>
        public string CheckoutUrl { get; set; } = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";

        /// <summary>主動查詢：後端問綠界「這筆到底付了沒」</summary>
        public string QueryUrl { get; set; } = "https://payment-stage.ecpay.com.tw/Cashier/QueryTradeInfo/V5";

        /// <summary>
        /// 後端網址，給「瀏覽器帶回（OrderResultURL）」用。
        /// 是客人的瀏覽器連過來，所以 localhost 就可以
        /// </summary>
        public string BackendBaseUrl { get; set; } = "https://localhost:7078";

        /// <summary>
        /// 給「綠界伺服器通知（ReturnURL）」用的對外網址。是綠界的伺服器連過來，所以要是網路上連得到的網址（ngrok）。
        /// 沒填就用 BackendBaseUrl：通知送不到你的電腦，但付款結果還是會靠「瀏覽器帶回＋主動查詢」完成
        /// </summary>
        public string? NotifyBaseUrl { get; set; }

        /// <summary>前台網址：付款完成後，把客人導回前台的付款結果頁</summary>
        public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

        /// <summary>付款對帳排程的設定（Ecpay:Reconciliation），都有預設值</summary>
        public EcpayReconciliationOptions Reconciliation { get; set; } = new();

        /// <summary>
        /// 特店編號、金鑰都有填，才算有開線上付款。
        /// 組員沒設定也要能跑網站：只是按「前往付款」時會回「線上付款尚未設定」
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(MerchantId)
            && !string.IsNullOrWhiteSpace(HashKey)
            && !string.IsNullOrWhiteSpace(HashIV);

        /// <summary>實際要給綠界的通知網址的開頭：有填 ngrok 網址就用 ngrok，沒填就用後端網址</summary>
        public string GetNotifyBaseUrl() =>
            string.IsNullOrWhiteSpace(NotifyBaseUrl) ? BackendBaseUrl : NotifyBaseUrl;
    }
}