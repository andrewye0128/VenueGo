namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 判定會員「現在能不能評論」某一筆訂單。
    /// <para>
    /// 【為什麼抽成 Service】撰寫頁表單、送出評論、我的評論、我的預約／我的票券的按鈕，
    /// 都要用同一套規則判斷。規則只寫在這裡一份，不會出現改了一邊、忘了另一邊的情況；
    /// 測試也不需要 HttpContext，給會員 Id 和假時間就能測。
    /// </para>
    /// <para>
    /// 【10/15 Reviews 重構】不再建評論憑證。資格每次都從來源資料即時判斷：
    /// <list type="bullet">
    ///   <item>現場評論：會員是那筆訂單某張票的持票人（EntryTicket.UserId），
    ///         而且那張票在預約時段內有有效入場紀錄（CheckInLog：Action＝入場、IsValid＝1）。
    ///         不看票券狀態：超時沒離場的票會停在「已失效」，但人確實來過。</item>
    ///   <item>預約評論：會員是訂購人（Orders.UserId），而且有已付款紀錄（Payments.PaidAt）。</item>
    ///   <item>期限：從第一次有效入場／最早一筆付款起算 14 天。</item>
    ///   <item>一位會員對一筆訂單，同一種評論只能一則（資料表的 UQ_Reviews_User_Order_Kind 也擋著）。</item>
    /// </list>
    /// 代價是資格會跟著來源資料變：組員把入場紀錄改成無效、訂單退款之後，原本能寫的就不能寫了。
    /// </para>
    /// <para>
    /// 【回傳】一律回傳判定結果，不丟例外、不回 HTTP 狀態碼。
    /// 要回 404、409 還是 410，由呼叫的 Controller 決定（那是 HTTP 的事，不是規則的事）。
    /// </para>
    /// </summary>
    public interface IReviewEligibilityService
    {
        /// <summary>現場評論：用訂單 Id 判定。userId 是 null（沒登入）一律回 NotFound。</summary>
        /// <param name="orderId">訂單的 OrderId（10/15 起；以前是 ReviewPerVisitId）</param>
        /// <param name="userId">目前登入的會員</param>
        Task<ReviewEligibility> CheckVisitAsync(int orderId, int? userId);

        /// <summary>預約評論：用訂單 Id 判定。userId 是 null（沒登入）一律回 NotFound。</summary>
        /// <param name="orderId">訂單的 OrderId</param>
        /// <param name="userId">目前登入的會員</param>
        Task<ReviewEligibility> CheckBookingAsync(int orderId, int? userId);

        /// <summary>
        /// 「我的評論」的待評論清單：資格成立、還沒過期、還沒寫過的現場和預約評論對象。
        /// 快到期的排前面。規則跟 CheckVisitAsync／CheckBookingAsync 的 Ok 完全一樣，只是一次查全部。
        /// </summary>
        Task<IReadOnlyList<ReviewTarget>> ListPendingAsync(int userId);
    }
}
