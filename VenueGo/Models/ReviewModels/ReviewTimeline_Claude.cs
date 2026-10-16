using System.Linq.Expressions;
using VenueGo.Models.Entities;

namespace VenueGo.Models.ReviewModels
{
    /// <summary>
    /// 「已經發生了沒」的判斷（10/12）：時間晚於網站時間的資料，一律當成還沒發生。
    /// <para>
    /// 【為什麼需要】專題發表要用時光機演示，一定會出現「未來」的資料：
    /// 例如撥到三天後寫評論、回覆，再撥回現在。這些資料如果照常顯示，不只畫面怪，
    /// 員工一點開就會寫入「早於評論建立時間」的已讀時間，撞到 CHK_Reviews_HandleTime_Logic，請求直接失敗。
    /// 所以評論子系統的清單、單筆查詢、操作前的讀取，都先用這裡的條件過濾。
    /// </para>
    /// <para>
    /// 【規則】一則評論身上任何一個時間點晚於現在，整則都當成還沒發生（不是只藏那個欄位）：
    /// 例如回覆時間在未來，只藏回覆的話，員工會以為還沒回、再回一次，又是一筆時間錯亂的資料。
    /// </para>
    /// <para>
    /// 10/15 Reviews 重構：評論表換成 Reviews，另外替三張檢視表各準備一份。
    /// 檢視表不能帶參數（時光機的現在時間傳不進去），所以這個條件一律由程式加在檢視表的查詢上。
    /// 評論憑證不存在了，原本的 VisitTicketIssuedBy／BookingTicketIssuedBy 一起拿掉：
    /// 「入場、付款的時間不晚於現在」改寫在資格判斷（ReviewEligibilityService）的查詢裡。
    /// </para>
    /// </summary>
    public static class ReviewTimeline
    {
        /// <summary>評論的每個時間點都不晚於 now。給 EF Core 查詢用（會翻成 SQL）。</summary>
        public static Expression<Func<Review, bool>> ReviewHappenedBy(DateTime now) => r =>
            r.CreatedAt <= now
            && (r.ReadAt == null || r.ReadAt <= now)
            && (r.RepliedAt == null || r.RepliedAt <= now)
            && (r.ReplyViewedAt == null || r.ReplyViewedAt <= now)
            && (r.SpamMarkedAt == null || r.SpamMarkedAt <= now);

        /// <summary>同一個規則，用在 v_ReviewDetail。</summary>
        public static Expression<Func<VReviewDetail, bool>> DetailHappenedBy(DateTime now) => r =>
            r.CreatedAt <= now
            && (r.ReadAt == null || r.ReadAt <= now)
            && (r.RepliedAt == null || r.RepliedAt <= now)
            && (r.ReplyViewedAt == null || r.ReplyViewedAt <= now)
            && (r.SpamMarkedAt == null || r.SpamMarkedAt <= now);

        /// <summary>同一個規則，用在 v_ReviewAdmin。</summary>
        public static Expression<Func<VReviewAdmin, bool>> AdminHappenedBy(DateTime now) => r =>
            r.CreatedAt <= now
            && (r.ReadAt == null || r.ReadAt <= now)
            && (r.RepliedAt == null || r.RepliedAt <= now)
            && (r.ReplyViewedAt == null || r.ReplyViewedAt <= now)
            && (r.SpamMarkedAt == null || r.SpamMarkedAt <= now);

        /// <summary>
        /// 同一個規則，用在 v_ReviewPublicCard。
        /// 這張檢視表本來就不含被下架的評論（SpamMarkedAt 不是 NULL 的整則不在裡面），所以少一個條件。
        /// 入場時間也一起檢查：入場在未來，代表這則評論在網站時間上「還沒到館」，不能上架。
        /// </summary>
        public static Expression<Func<VReviewPublicCard, bool>> PublicCardHappenedBy(DateTime now) => r =>
            r.CreatedAt <= now
            && (r.ReadAt == null || r.ReadAt <= now)
            && (r.RepliedAt == null || r.RepliedAt <= now)
            && (r.ReplyViewedAt == null || r.ReplyViewedAt <= now)
            && (r.CheckInAt == null || r.CheckInAt <= now);

        /// <summary>
        /// 同一個規則，用在已經讀進記憶體的評論。
        /// 直接執行上面那份條件，不另寫一份：兩份各寫各的，改了一邊忘了另一邊就會不一致。
        /// </summary>
        public static bool HasHappened(Review review, DateTime now)
            => ReviewHappenedBy(now).Compile().Invoke(review);
    }
}
