namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{
    /// <summary>
    /// 首頁「顧客評價」輪播（規格 2-11，10/15）。
    /// <para>Summary：全部公開評價的平均分數與總則數（跟評論專區「全部時間」一致）。</para>
    /// <para>Items：最多 12 張卡片，順序已經打亂，前台照順序顯示就好。</para>
    /// </summary>
    public sealed record FeaturedReviewsDto(
        RatingSummaryDto Summary,
        IReadOnlyList<PublicReviewCardDto> Items);
}
