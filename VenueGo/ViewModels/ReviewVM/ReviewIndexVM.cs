namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 顧客端評論專區的時間範圍。值與規劃檔一致，不要改字串。
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
    }

    /// <summary>排序方式。</summary>
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
    }

    /// <summary>運動類型分頁的一個選項。null 代表「全部類型」。</summary>
    public record SportTabVM(int? SportTypeId, string Name);

    /// <summary>
    /// 星等分布。同時是畫面上的篩選控制項。
    ///
    /// ⚠️ 這些數字是用「上架條件 + 類型 + 時間範圍 + 有內容」算的，
    ///    但「不」套用星等篩選——否則點了 5 星之後分布圖只會剩一條，
    ///    使用者就失去切換回去的參照了。
    /// </summary>
    public class StarDistributionVM
    {
        public int Star5 { get; init; }
        public int Star4 { get; init; }
        public int Star3 { get; init; }
        public int Star2 { get; init; }
        public int Star1 { get; init; }

        /// <summary>用星數取筆數，View 跑迴圈時用，不必寫五次 if。</summary>
        public int CountOf(int star) => star switch
        {
            5 => Star5,
            4 => Star4,
            3 => Star3,
            2 => Star2,
            1 => Star1,
            _ => 0
        };

        public int Total => Star5 + Star4 + Star3 + Star2 + Star1;

        /// <summary>
        /// 平均分數。
        /// ⚠️ Total 為 0 時回 0——新場館或篩選後無資料會除以零。
        /// </summary>
        public double Average => Total == 0
            ? 0
            : (double)(Star5 * 5 + Star4 * 4 + Star3 * 3 + Star2 * 2 + Star1) / Total;

        /// <summary>某星等佔比，給長條圖的寬度用。同樣要處理 Total 為 0。</summary>
        public int PercentOf(int star) => Total == 0 ? 0 : CountOf(star) * 100 / Total;
    }

    /// <summary>
    /// 顧客端評論專區（CReview/Index）整頁的資料。
    /// </summary>
    public class ReviewIndexVM
    {
        // ── 篩選狀態（從查詢字串來，要原樣帶回 View）──

        /// <summary>運動類型分頁。null = 全部類型。</summary>
        public int? SportTypeId { get; init; }

        public string TimeRange { get; init; } = ReviewRange.Default;

        /// <summary>點分布圖某一條時帶入。null = 不依星等篩選。</summary>
        public int? Star { get; init; }

        /// <summary>只看有留言的，排除「僅評分」的評論。</summary>
        public bool HasContentOnly { get; init; }

        public string Sort { get; init; } = ReviewSort.Default;

        // ── 畫面用 ──

        /// <summary>分頁列。第一個是「全部類型」。</summary>
        public List<SportTabVM> SportTabs { get; init; } = new();

        public StarDistributionVM Distribution { get; init; } = new();

        /// <summary>
        /// 預設給一個空清單，View 裡就算沒資料也能安全跑 foreach，
        /// 不必每次都先判斷 null。
        /// </summary>
        public List<ReviewCardVM> Items { get; init; } = new();

        // ── 分頁（期中尚未實作，欄位先留著）──
        public int Page { get; init; } = 1;
        public int TotalPages { get; init; }

        /// <summary>空狀態判定。</summary>
        public bool IsEmpty => Items.Count == 0;

        /// <summary>有沒有套用任何非預設條件。用來決定「清除篩選」要不要出現。</summary>
        public bool HasFilter =>
            SportTypeId != null
            || TimeRange != ReviewRange.Default
            || Star != null
            || HasContentOnly
            || Sort != ReviewSort.Default;
    }
}
