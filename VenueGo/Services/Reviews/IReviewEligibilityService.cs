using VenueGo.Models.Entities;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 判定會員「現在能不能評論」某一張憑證。
    /// <para>
    /// 【為什麼抽成 Service】撰寫頁表單、送出評論、我的評論，以及之後「我的評論」頁的待評論清單，
    /// 都要用同一套規則判斷「存在、是本人的、還沒評過、還沒過期」。規則只寫在這裡一份，
    /// 不會出現改了一邊、忘了另一邊的情況；測試也不需要 HttpContext，給會員 Id 和假時間就能測。
    /// </para>
    /// <para>
    /// 【回傳】一律回傳判定結果，不丟例外、不回 HTTP 狀態碼。
    /// 要回 404、409 還是 410，由呼叫的 Controller 決定（那是 HTTP 的事，不是規則的事）。
    /// </para>
    /// </summary>
    public interface IReviewEligibilityService
    {
        /// <summary>
        /// 現場評論：用憑證 Id 判定。只有憑證上的會員本人（ReviewPerVisit.UserId）判得過。
        /// <para>userId 是 null（沒登入）一律回 NotFound。</para>
        /// </summary>
        /// <param name="reviewPerVisitId">現場評論憑證的 ReviewPerVisitId</param>
        /// <param name="userId">目前登入的會員</param>
        Task<ReviewEligibility<ReviewPerVisit>> CheckVisitAsync(int reviewPerVisitId, int? userId);

        /// <summary>
        /// 預約評論：用訂單 Id 判定。只有訂購人本人判得過。
        /// <para>
        /// 查不到憑證時，如果訂單是本人的而且已經付款，會當場補建一張（付款系統可能漏了呼叫工廠）。
        /// userId 是 null（沒登入）一律回 NotFound。
        /// </para>
        /// </summary>
        /// <param name="orderId">訂單的 OrderId</param>
        /// <param name="userId">目前登入的會員</param>
        Task<ReviewEligibility<ReviewPerBooking>> CheckBookingAsync(int orderId, int? userId);

        /// <summary>
        /// 「我的評論」的待評論清單：本人的、還沒評過、還沒過期的現場和預約憑證（10/10）。
        /// <para>
        /// 規則跟 CheckVisitAsync／CheckBookingAsync 的「可以評論（Ok）」完全一樣，只是一次查全部。
        /// </para>
        /// <para>
        /// 10/11 起，查詢之前會先補建本人漏掉的現場、預約憑證（IReviewTicketBackfill），所以這個方法可能寫入資料。
        /// 補建的期限從報到／付款時間起算、已過期的不建，所以不會讓早就過期的到場或訂單突然又能評。
        /// </para>
        /// </summary>
        /// <param name="userId">目前登入的會員</param>
        Task<PendingReviewTickets> ListPendingAsync(int userId);
    }
}
