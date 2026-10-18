namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>
    /// 對回覆表態（規格 2-7）的請求。10/10 由 SatisfactionRequest 改名。
    /// 用 byte?：沒送的時候是 null，不會被當成 0（不滿意）。
    /// </summary>
    public sealed record SatisfactionRequestDto(byte? Satisfaction);
}
