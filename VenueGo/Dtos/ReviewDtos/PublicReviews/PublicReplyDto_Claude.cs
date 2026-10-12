using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>公開卡片上的館方回覆。⚠️ 沒有回覆的員工姓名。</summary>
    public sealed record PublicReplyDto(string Content, TimeView RepliedAt);
}
