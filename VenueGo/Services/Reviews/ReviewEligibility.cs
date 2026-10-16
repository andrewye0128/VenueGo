using VenueGo.Models.Entities;

namespace VenueGo.Services.Reviews
{
    /// <summary>「這筆訂單現在能不能評論」的判定結果。</summary>
    public enum ReviewEligibilityState
    {
        /// <summary>可以評論。</summary>
        Ok,

        /// <summary>
        /// 沒有資格（不是本人的訂單、沒有有效入場、沒有付款紀錄⋯），或訂單根本不存在。
        /// 幾種情況刻意不分開：不讓人從回應分辨出「這筆訂單存在，只是不是你的」。
        /// </summary>
        NotFound,

        /// <summary>超過可以評論的期限。</summary>
        Expired,

        /// <summary>已經評論過了（ExistingReview 有值）。</summary>
        AlreadyReviewed,
    }

    /// <summary>
    /// 判定結果（10/15 Reviews 重構：不再有憑證，改成「評論對象」）。
    /// <para>Target：資格成立時的評論對象（Ok、Expired 時有值）。</para>
    /// <para>ExistingReview：已經寫過的那則評論，只有 AlreadyReviewed 時有值。</para>
    /// </summary>
    public sealed record ReviewEligibility(
        ReviewEligibilityState State,
        ReviewTarget? Target,
        Review? ExistingReview)
    {
        /// <summary>沒有資格（或不是本人的）。</summary>
        public static ReviewEligibility NotFound { get; } = new(ReviewEligibilityState.NotFound, null, null);
    }
}
