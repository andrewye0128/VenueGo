namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>
    /// 評論專區的時間範圍。值是 API 的查詢參數（?range=7days），不要改字串。
    /// 10/10 從 ViewModels/ReviewViewModels/ReviewIndexVM.cs 搬過來（內容沒改）。
    /// </summary>
    public static class ReviewRange
    {
        public const string Week  = "7days";
        public const string Month = "30days";
        public const string Year  = "year";
        public const string All   = "all";

        /// <summary>預設一年內。顧客端跟館方端不同——館方要處理近期的，
        /// 顧客是來「看這個場館評價如何」的，範圍太窄反而看不出全貌。</summary>
        public const string Default = Year;

        /// <summary>往前推幾天。null = 不篩選。</summary>
        public static int? DaysOf(string range) => range switch
        {
            Week  => 7,
            Month => 30,
            Year  => 365,
            _     => null
        };

        public static readonly (string Value, string Text)[] Options =
        {
            (Week,  "一週內"),
            (Month, "一個月內"),
            (Year,  "一年內"),
            (All,   "全部")
        };

        /// <summary>不認得的值一律改用預設值（不回錯誤）。10/10 從 CReviewApiController 搬過來。</summary>
        public static string Normalize(string? range) => range switch
        {
            Week or Month or Year or All => range,
            _                            => Default
        };
    }
}
