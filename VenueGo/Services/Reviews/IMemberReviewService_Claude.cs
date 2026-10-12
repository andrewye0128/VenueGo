using VenueGo.Dtos.ReviewDtos.MyReviews;
using VenueGo.Dtos.ReviewDtos.WriteReview;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 會員自己的評論：撰寫頁設定、送出、查看、已讀、切換公開、表態，以及員工的顧客視角預覽。
    /// <para>
    /// 【為什麼抽成 Service】10/10 從 CReviewApiController 搬出來。
    /// Controller 只剩「接參數 → 呼叫 → 把結果翻成 HTTP 回應」；
    /// 規則（誰能看、什麼狀態能做什麼、存檔＋建立預審）都在這裡，測試不需要 HttpContext。
    /// 之後「我的評論」頁的已評論清單也會加在這裡。
    /// </para>
    /// <para>
    /// 【回傳】一律回傳 <see cref="MemberReviewResult{T}"/>／<see cref="MemberReviewOutcome"/>，
    /// 不丟例外、不回 IActionResult。
    /// </para>
    /// <para>
    /// 【參數】reviewPerVisitId：現場評論憑證 Id；orderId：預約評論的訂單 Id；
    /// userId：目前登入的會員，null（沒登入）一律當成 NotFound。
    /// </para>
    /// </summary>
    public interface IMemberReviewService
    {
        // ── 撰寫（規格 2-2、2-3）──────────────────────────────

        /// <summary>現場評論的撰寫頁設定。不能評論時回 NotFound／Expired／AlreadyReviewed。</summary>
        Task<MemberReviewResult<WriteFormDto>> GetVisitFormAsync(int reviewPerVisitId, int? userId);

        /// <summary>預約評論的撰寫頁設定。不能評論時回 NotFound／Expired／AlreadyReviewed。</summary>
        Task<MemberReviewResult<WriteFormDto>> GetBookingFormAsync(int orderId, int? userId);

        /// <summary>
        /// 送出現場評論：重新判定資格 → 存評論 → 建立預審紀錄（預審失敗不影響送出）。
        /// 不能評論時回 NotFound／Expired／AlreadyReviewed，什麼都不存。
        /// </summary>
        Task<MemberReviewOutcome> CreateVisitReviewAsync(int reviewPerVisitId, int? userId, ReviewCreateForVisitDto input);

        /// <summary>送出預約評論。一律存成不公開、不匿名。其餘同 <see cref="CreateVisitReviewAsync"/>。</summary>
        Task<MemberReviewOutcome> CreateBookingReviewAsync(int orderId, int? userId, ReviewCreateForBookingDto input);

        // ── 查看（規格 2-4、2-8），純讀取 ───────────────────────

        /// <summary>我的現場評論。還沒評論過也回 NotFound。不會記錄已讀。</summary>
        Task<MemberReviewResult<MyReviewDto>> GetVisitReviewAsync(int reviewPerVisitId, int? userId);

        /// <summary>我的預約評論。還沒評論過也回 NotFound。不會記錄已讀。</summary>
        Task<MemberReviewResult<MyReviewDto>> GetBookingReviewAsync(int orderId, int? userId);

        /// <summary>
        /// 員工以顧客視角預覽，格式跟「我的評論」一樣。用 ReviewId 查，不檢查是誰寫的
        /// （權限由 Controller 的 [Authorize(Roles = BackOffice)] 把關）。不會記錄已讀。
        /// </summary>
        Task<MemberReviewResult<MyReviewDto>> GetPreviewAsync(int reviewId);

        // ── 寫入（規格 2-5 ～ 2-7）────────────────────────────

        /// <summary>記錄已讀館方回覆。只在「有回覆而且還沒記錄過」時寫入；重複呼叫不會蓋掉第一次的時間。</summary>
        Task<MemberReviewResult<ReplyViewedDto>> MarkVisitReplyViewedAsync(int reviewPerVisitId, int? userId);

        /// <summary>同 <see cref="MarkVisitReplyViewedAsync"/>，預約評論版。</summary>
        Task<MemberReviewResult<ReplyViewedDto>> MarkBookingReplyViewedAsync(int orderId, int? userId);

        /// <summary>切換現場評論公開。被標為垃圾的回 SpamMarked。只有現場評論有這個操作。</summary>
        Task<MemberReviewResult<VisibilityResultDto>> SetVisitVisibilityAsync(int reviewPerVisitId, int? userId, bool isPublic);

        /// <summary>
        /// 對館方回覆表態（0 不滿意、1 普通、2 滿意）。還沒回覆回 NoReply、表態過回 AlreadyRated。
        /// satisfaction 的範圍由 Controller 先檢查。
        /// </summary>
        Task<MemberReviewResult<SatisfactionResultDto>> SetVisitSatisfactionAsync(int reviewPerVisitId, int? userId, byte satisfaction);

        /// <summary>同 <see cref="SetVisitSatisfactionAsync"/>，預約評論版。</summary>
        Task<MemberReviewResult<SatisfactionResultDto>> SetBookingSatisfactionAsync(int orderId, int? userId, byte satisfaction);
    }
}
