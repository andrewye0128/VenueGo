namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>這次查詢實際套用的篩選條件（不認得的值已經換成預設值）。</summary>
    /// <remarks>10/17：拿掉 HasContentOnly（改成「沒選星等就只列有文字的」，見 PublicReviewListDto.RatingOnlyHidden），加上 Mention。</remarks>
    public sealed record ReviewFilterDto(int? SportTypeId, string Range, int? Star, string Mention, string Sort);
}
