using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>「我的評論」上的館方回覆，含已讀與表態狀態。</summary>
    public sealed record MyReplyDto(
        string Content,
        TimeView RepliedAt,
        bool IsViewed,
        byte? Satisfaction,          // 0 不滿意、1 普通、2 滿意；還沒表態是 null
        bool CanRateSatisfaction);
}
