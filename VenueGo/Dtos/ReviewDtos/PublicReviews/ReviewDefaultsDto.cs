namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>篩選的預設值，前端「清除篩選」時用。</summary>
    public sealed record ReviewDefaultsDto(string Range, string Mention, string Sort);
}
