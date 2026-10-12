namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>
    /// 切換公開（規格 2-6）的請求。10/10 由 VisibilityRequest 改名。
    /// 用 bool? 而不是 bool：前端忘了送這個欄位時，bool 會默默變成 false（＝ 關掉公開），
    /// bool? 則是 null，後端才分得出「沒送」和「送了 false」。
    /// </summary>
    public sealed record VisibilityRequestDto(bool? IsPublic);
}
