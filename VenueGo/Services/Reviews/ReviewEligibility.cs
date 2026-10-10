using VenueGo.Models.Entities;

namespace VenueGo.Services.Reviews
{
    /// <summary>「這張憑證現在能不能評論」的判定結果。</summary>
    public enum ReviewEligibilityState
    {
        /// <summary>可以評論。</summary>
        Ok,

        /// <summary>
        /// 查無憑證，或憑證不是本人的。
        /// 兩種情況刻意不分開：不讓人從回應分辨出「這張憑證存在，只是不是你的」。
        /// </summary>
        NotFound,

        /// <summary>超過可以評論的期限。</summary>
        Expired,

        /// <summary>已經評論過了（ExistingReview 有值）。</summary>
        AlreadyReviewed,
    }

    /// <summary>
    /// 判定結果。現場評論、預約評論共用同一個形狀，TTicket 是各自的憑證型別。
    /// <para>Ticket：憑證本身，NotFound 時是 null。</para>
    /// <para>ExistingReview：已經寫過的那則評論，只有 AlreadyReviewed 時有值。</para>
    /// </summary>
    public sealed record ReviewEligibility<TTicket>(
        ReviewEligibilityState State,
        TTicket? Ticket,
        ReviewMain? ExistingReview)
        where TTicket : class
    {
        /// <summary>查無憑證（或不是本人的）。</summary>
        public static ReviewEligibility<TTicket> NotFound { get; } = new(ReviewEligibilityState.NotFound, null, null);
    }
}
