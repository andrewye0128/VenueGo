namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>
    /// 評論專區的排序方式。值是 API 的查詢參數（?sort=newest），不要改字串。
    /// 10/10 從 ViewModels/ReviewViewModels/ReviewIndexVM.cs 搬過來（內容沒改）。
    /// </summary>
    public static class ReviewSort
    {
        public const string Newest  = "newest";
        public const string Highest = "highest";
        public const string Lowest  = "lowest";

        public const string Default = Highest;

        public static readonly (string Value, string Text)[] Options =
        {
            (Newest,  "最新"),
            (Highest, "評分最高"),
            (Lowest,  "評分最低")
        };

        /// <summary>不認得的值一律改用預設值（不回錯誤）。10/10 從 CReviewApiController 搬過來。</summary>
        public static string Normalize(string? sort) => sort switch
        {
            Newest or Highest or Lowest => sort,
            _                           => Default
        };
    }
}
