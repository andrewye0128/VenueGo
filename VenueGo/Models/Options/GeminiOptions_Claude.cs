namespace VenueGo.Models.Options
{
    /// <summary>
    /// Google Gemini 的連線設定。值放在 Secrets.json（管理使用者祕密），不放在任何會進 Git 的檔案。
    ///
    ///   "Gemini": {
    ///     "ApiKey": "…",
    ///     "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai/",
    ///     "ScreeningModel": "gemini-3.5-flash-lite"
    ///   }
    ///
    /// 用的是 Gemini 的「OpenAI 相容」介面：之後要換別家、或換成本機模型，
    /// 只要改 BaseUrl 和模型名稱，程式碼不用動。
    /// </summary>
    public sealed class GeminiOptions
    {
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

        /// <summary>
        /// 評論預審用的模型。用固定版本名稱，不要用 *-latest 這種別名：
        /// ReviewScreening.AiModel 會記下每則評論是哪個模型分析的，別名會讓這筆紀錄失去意義。
        /// </summary>
        public string ScreeningModel { get; set; } = string.Empty;

        /// <summary>
        /// 沒設定金鑰或模型，就當作 AI 預審沒開。
        /// 組員 clone 下來沒有金鑰也要能跑：評論照常送出、照常進清單，只是沒有 AI 標籤。
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ScreeningModel);
    }
}
