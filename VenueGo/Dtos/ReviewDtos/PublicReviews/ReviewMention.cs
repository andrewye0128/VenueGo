namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{
    /// <summary>
    /// 評論專區的「提及」篩選（10/17）：只看提到場地設施、或提到人員服務的評論。
    /// 值是 API 的查詢參數（?mention=venue），不要改字串。寫法照 <see cref="ReviewRange"/>。
    /// </summary>
    public static class ReviewMention
    {
        public const string All   = "all";
        public const string Venue = "venue";
        public const string Staff = "staff";

        /// <summary>預設全部。</summary>
        public const string Default = All;

        // 文字跟前台評論卡上的標籤一致（constants/review.js 的 MENTION_TAGS）
        public static readonly (string Value, string Text)[] Options =
        {
            (All,   "全部"),
            (Venue, "場地設施"),
            (Staff, "人員服務")
        };

        /// <summary>不認得的值一律改用預設值（不回錯誤）。</summary>
        public static string Normalize(string? mention) => mention switch
        {
            All or Venue or Staff => mention,
            _                     => Default
        };
    }
}
