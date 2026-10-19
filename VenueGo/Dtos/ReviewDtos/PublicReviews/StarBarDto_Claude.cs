namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>星等分布的一條長條：幾顆星、幾則、佔幾 %。</summary>
    public sealed record StarBarDto(int Star, int Count, int Percent);
}
