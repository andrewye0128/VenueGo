using VenueGo.Models.ReviewModels;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 一個「可以評論的對象」：哪位會員、哪筆訂單、哪一種評論（10/15 Reviews 重構）。
    /// <para>
    /// 以前資格是一張預先建好的憑證（ReviewPerVisit／ReviewPerBooking）；現在不建憑證，
    /// 每次都從來源資料（票券、入場紀錄、訂單、付款）即時算出來，算出來的結果就是這個 record。
    /// 它只存在記憶體裡，不寫進資料庫；寫評論時才把 OrderId、TicketId 抄進 Reviews。
    /// </para>
    /// </summary>
    /// <param name="Kind">現場或預約</param>
    /// <param name="OrderId">評論的是哪一筆訂單</param>
    /// <param name="UserId">誰可以評（現場＝持票人 EntryTicket.UserId；預約＝訂購人 Orders.UserId）</param>
    /// <param name="TicketId">現場評論才有：判斷資格用的那張票（預約時段內最早有效入場的那張）</param>
    /// <param name="StartedAt">期限的起算點：現場＝第一次有效入場；預約＝最早一筆付款</param>
    /// <param name="ExpiredAt">StartedAt＋14 天（ReviewPolicy.TicketValidDays）；現在 ≥ 這個時間就過期</param>
    public sealed record ReviewTarget(
        ReviewKindCode Kind,
        int OrderId,
        int UserId,
        int? TicketId,
        DateTime StartedAt,
        DateTime ExpiredAt);
}
