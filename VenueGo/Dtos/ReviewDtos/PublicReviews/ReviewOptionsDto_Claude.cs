using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>篩選的選項：時間、排序是下拉選單，Mentions（10/17）是篩選分頁。</summary>
    public sealed record ReviewOptionsDto(
        IReadOnlyList<OptionItem> Ranges, IReadOnlyList<OptionItem> Mentions, IReadOnlyList<OptionItem> Sorts);
}
