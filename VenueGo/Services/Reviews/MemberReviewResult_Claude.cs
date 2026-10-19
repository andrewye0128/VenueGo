namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 會員評論操作的結果種類（<see cref="IMemberReviewService"/> 用）。
    /// Service 只回「發生了什麼」，要回 404、409 還是 410 由 Controller 決定。
    /// </summary>
    public enum MemberReviewOutcome
    {
        /// <summary>成功。</summary>
        Ok,

        /// <summary>
        /// 查無憑證、憑證不是本人的，或（查看／操作已寫的評論時）還沒評論過。
        /// 幾種情況刻意不分開：不讓人從回應分辨出「這張憑證存在，只是不是你的」。
        /// </summary>
        NotFound,

        /// <summary>超過可以評論的期限（撰寫、送出時）。</summary>
        Expired,

        /// <summary>這張憑證已經評論過了（撰寫、送出時）。</summary>
        AlreadyReviewed,

        /// <summary>館方還沒回覆，不能表態。</summary>
        NoReply,

        /// <summary>已經表態過了，每則評論只能表態一次。</summary>
        AlreadyRated,

        /// <summary>評論已被館方標為垃圾（下架），不能切換公開。</summary>
        SpamMarked,
    }

    /// <summary>
    /// 會員評論操作的結果：種類＋成功時的資料。
    /// Data 只有在 Outcome 是 Ok 時才有意義。
    /// </summary>
    public sealed record MemberReviewResult<T>(MemberReviewOutcome Outcome, T? Data)
    {
        public static MemberReviewResult<T> Ok(T data) => new(MemberReviewOutcome.Ok, data);

        public static MemberReviewResult<T> Fail(MemberReviewOutcome outcome) => new(outcome, default);
    }
}
