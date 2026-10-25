namespace VenueGo.Dtos.ReviewDtos.MyReviews
{
    /// <summary>
    /// 「我的評論 › 待評論」的一張卡片（10/10）。
    /// Kind＋OrderId 就是撰寫頁網址的兩段：/reviews/{kind}/{orderId}/write。
    /// ⚠️ 屬性名稱就是 JSON 的欄位名稱，改名等於改規格，前端要跟著改。
    /// </summary>
    public sealed record PendingReviewItemDto(
        string Kind,                 // ReviewKind 的值：visit／booking
        int OrderId,                 // 訂單 Id（現場、預約都是）。10/25 從 Ticket 改名，JSON 欄位從 ticket 變成 orderId，前台一起改
        ReviewContextDto Context,    // 兩行字：現場是場地／使用時段，預約是訂單編號
        ReviewDeadlineDto Deadline);
}
