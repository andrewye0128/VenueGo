using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>下拉選單的選項。</summary>
    public sealed record ReviewOptionsDto(IReadOnlyList<OptionItem> Ranges, IReadOnlyList<OptionItem> Sorts);
}
