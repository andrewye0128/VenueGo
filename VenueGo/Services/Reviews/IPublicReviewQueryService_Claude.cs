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
        /// </summary>
        Task<PublicReviewListDto> GetPublicReviewsAsync(
            int? sportTypeId, string? range, int? star, bool hasContentOnly, string? sort);
    }
}
