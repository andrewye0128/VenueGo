namespace VenueGo.Models.Options
{
    public class AiDraftOptions
    {
        public const string SectionName = "AiDraft";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "<https://api.openai.com/v1>";
        public string Model { get; set; } = string.Empty;

        /// <summary>草稿字數上限，超過就截掉。太長員工根本不會看，直接重寫。</summary>
        public int MaxChars { get; set; } = 200;

        /// <summary>沒設定金鑰就當作功能沒開，按鈕不顯示。</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
    }
}