namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>記錄已讀（規格 2-5）的回應：目前是否已記錄為已讀。10/10 由 ReplyViewedResult 改名。</summary>
    public sealed record ReplyViewedDto(bool Viewed);
}
