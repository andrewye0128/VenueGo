namespace VenueGo.Dtos.ReviewDtos.MyReviews
{
    /// <summary>
    /// 「我的評論 › 待評論」的一張卡片（10/10）。
    /// Kind＋Ticket 就是撰寫頁網址的兩段：/reviews/{kind}/{ticket}/write。
    /// ⚠️ 屬性名稱就是 JSON 的欄位名稱，改名等於改規格，前端要跟著改。
    /// </summary>
    public sealed record PendingReviewItemDto(
        string Kind,                 // ReviewKind 的值：visit／booking
        int Ticket,                  // 訂單 Id（10/15 起現場、預約都是；以前現場評論是憑證 Id）。名稱沿用，前台不用改
        ReviewContextDto Context,    // 兩行字：現場是場地／使用時段，預約是訂單編號
        ReviewDeadlineDto Deadline);
}
