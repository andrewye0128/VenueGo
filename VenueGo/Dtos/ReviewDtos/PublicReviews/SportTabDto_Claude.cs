namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>
    /// 運動類型分頁的一個選項。SportTypeId 是 null 代表「全部類型」。
    /// 10/10 由 ViewModels/ReviewViewModels 的 SportTabVM 改名搬過來（JSON 形狀沒變）。
    /// </summary>
    public sealed record SportTabDto(int? SportTypeId, string Name);
}
