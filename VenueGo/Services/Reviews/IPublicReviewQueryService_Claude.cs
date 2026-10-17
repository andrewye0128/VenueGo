using VenueGo.Dtos.ReviewDtos.PublicReviews;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 評論專區（公開評論）的查詢：上架條件、篩選、排序、星等分布。
    /// <para>
    /// 【為什麼抽成 Service】10/10 從 CReviewApiController 搬出來。
    /// 「哪些評論可以公開」這條規則只能有一份；首頁的評價卡 slide 之後也要用同一個上架條件，
    /// 放在 Service 裡兩邊都能呼叫，不會一邊改了、另一邊漏改。
    /// </para>
    /// </summary>
    public interface IPublicReviewQueryService
    {
        /// <summary>
        /// 評論專區的完整資料（規格 2-1）。
        /// 不認得的 range、sort 一律改用預設值；star 不在 1～5 當成不篩選。
        /// take（10/13）：最多回傳幾則卡片，null 代表全部；只限制卡片，不影響星等分布與平均。
        /// </summary>
        Task<PublicReviewListDto> GetPublicReviewsAsync(
            int? sportTypeId, string? range, int? star, bool hasContentOnly, string? sort, int? take = null);

        /// <summary>
        /// 首頁「顧客評價」輪播（規格 2-11，10/16 改版）：最多 12 則 3 星以上的評價，隨機順序。
        /// <para>
        /// 挑選規則寫在 PublicReviewQueryService.PickFeatured：有館方回覆、30 字以上的優先，只給星等的最後。一則都沒有時回空清單。
        /// </para>
        /// <para>每次呼叫結果都不一樣，所以這支 API 不能被快取。</para>
        /// </summary>
        Task<FeaturedReviewsDto> GetFeaturedReviewsAsync();
    }
}
