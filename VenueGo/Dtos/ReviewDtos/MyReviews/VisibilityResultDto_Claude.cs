namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>切換公開（規格 2-6）的回應：切換後的狀態。10/10 由 VisibilityResult 改名。</summary>
    public sealed record VisibilityResultDto(bool IsPublic);
}
