namespace VenueGo.Dtos.ReviewDtos.MyReviews
{
    /// <summary>
    /// 我的預約、我的票券上的評論按鈕（規格 2-12，10/15）：一筆訂單一列，只列「有按鈕要顯示」的訂單。
    /// <para>
    /// VisitAction／BookingAction 的值：
    /// <list type="bullet">
    ///   <item>"write"：顯示「立刻評論」（資格成立、還沒過期、還沒寫過）</item>
    ///   <item>"view"：顯示「查看評論」（已經寫過，而且不是時光機撥回來之後的「未來」評論）</item>
    ///   <item>null：不顯示按鈕</item>
    /// </list>
    /// </para>
    /// <para>
    /// 我的預約用 ReservationId 對到每一列、顯示預約評論的按鈕；
    /// 我的票券用 VisitTicketId 對到每一張票、顯示現場評論的按鈕。
    /// 一位會員在一筆訂單可能拿著好幾張票，只有 VisitTicketId 那一張顯示按鈕（每筆訂單只能評一次）。
    /// </para>
    /// </summary>
    public sealed record ReviewStatusItemDto(
        int OrderId,
        int? ReservationId,
        string? VisitAction,
        int? VisitTicketId,      // "write"：判斷資格用的那張票；"view"：寫評論時用的那張票
        string? BookingAction);

    /// <summary>ReviewStatusItemDto 的 Action 值。</summary>
    public static class ReviewStatusAction
    {
        public const string Write = "write";
        public const string View = "view";
    }
}
