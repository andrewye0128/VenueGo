namespace VenueGo.Models.ReviewModels
{
    /// <summary>
    /// Reviews.ReviewKind 的值（10/15 Reviews 重構）。資料表用 CHECK 限制只能是 1 或 2。
    /// <para>
    /// 名字不叫 ReviewKind：Dtos.ReviewDtos.ReviewKind 已經是網址用的字串常數（"visit"、"booking"），
    /// 兩個同名的話 using 兩個命名空間時會撞名。兩者的對應用 <see cref="ReviewKindCodeExtensions"/>。
    /// </para>
    /// </summary>
    public enum ReviewKindCode : byte
    {
        /// <summary>現場評論：到館使用之後寫的，可以公開。</summary>
        Visit = 1,

        /// <summary>預約評論：付款之後寫的，只給館方看。</summary>
        Booking = 2,
    }

    public static class ReviewKindCodeExtensions
    {
        /// <summary>資料表的值 → 網址、API 回應用的字串（"visit"／"booking"）。</summary>
        public static string ToRouteKind(this ReviewKindCode kind) => kind switch
        {
            ReviewKindCode.Visit => Dtos.ReviewDtos.ReviewKind.Visit,
            ReviewKindCode.Booking => Dtos.ReviewDtos.ReviewKind.Booking,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的評論種類")
        };

        /// <summary>Reviews.ReviewKind（byte）→ 網址用的字串。</summary>
        public static string ToRouteKind(byte kind) => ((ReviewKindCode)kind).ToRouteKind();
    }
}
