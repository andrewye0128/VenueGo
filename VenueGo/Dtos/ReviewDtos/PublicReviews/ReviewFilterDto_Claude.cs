namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>這次查詢實際套用的篩選條件（不認得的值已經換成預設值）。</summary>
    public sealed record ReviewFilterDto(int? SportTypeId, string Range, int? Star, bool HasContentOnly, string Sort);
}
