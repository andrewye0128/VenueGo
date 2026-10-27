namespace VenueGo.Models.Options
{
    /// <summary>
    /// 中央氣象署開放資料的連線設定（10/18，首頁 TopBar 即時氣象）。
    /// 授權碼放在 Secrets.json（管理使用者祕密），不放在任何會進 Git 的檔案；其他值有預設，不用設定：
    ///
    ///   "Cwa": {
    ///     "ApiKey": "CWA-…"
    ///   }
    ///
    /// 組員 clone 下來沒有授權碼也要能跑：TopBar 不顯示天氣，其他功能照常。
    /// </summary>
    public sealed class CwaOptions
    {
        public const string SectionName = "Cwa";

        public string ApiKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://opendata.cwa.gov.tw/api/v1/rest/datastore/";

        // 10/18：組內決定運動中心在高雄（原本是臺北：O-A0003-001 臺北站 466920、F-D0047-063 中正區）

        /// <summary>
        /// 現在天氣：局屬氣象站觀測（O-A0003-001）的「高雄」站。2022 年起搬到楠梓區的新站，代碼是 467441；
        /// 舊站代碼（467440）已經查不到資料。
        /// </summary>
        public string ObservationDataId { get; set; } = "O-A0003-001";
        public string StationId { get; set; } = "467441";

        /// <summary>
        /// 三天預報：高雄市鄉鎮未來一週預報（F-D0047-067）的楠梓區（高雄氣象站在楠梓區）。
        /// 用一週版、不用三天版（F-D0047-065）：一週版直接給每 12 小時的最高、最低溫和白天／晚上的天氣，
        /// 三天版只有逐時的溫度和逐 3 小時的天氣，最高最低溫要自己從整點的溫度挑，天氣也只代表那 3 小時。
        /// 兩種解析程式都看得懂（CwaWeatherParser），要換的話只改這裡。
        /// </summary>
        public string ForecastDataId { get; set; } = "F-D0047-067";
        public string LocationName { get; set; } = "楠梓區";

        /// <summary>畫面上顯示的地名。</summary>
        public string DisplayName { get; set; } = "高雄市";

        /// <summary>多久向氣象署要一次（分鐘）。觀測大約每 10 分鐘更新，預報一天更新幾次，10 分鐘一次就夠新。</summary>
        public int CacheMinutes { get; set; } = 10;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
