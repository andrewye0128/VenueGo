using VenueGo.Dtos;
using VenueGo.Dtos.ReviewDtos;
using VenueGo.Dtos.ReviewDtos.MyReviews;
using VenueGo.Dtos.ReviewDtos.PublicReviews;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.ReviewScreening;

namespace VenueGo.Mappers
{
    // ════════════════════════════════════════════════════════════════
    //  顧客端評論：資料表 → 給前台的 DTO（9/29 從 CReviewApiController 抽出來）
    //
    //  組員開發注意事項：「給前台的 API 不要直接回傳 Entity，要轉成 Dtos/ 裡的格式，
    //  轉換的程式放在 Mappers/」。
    //
    //  這裡只做「轉換」，不查資料庫：要用到的名稱（顯示名稱、場地名稱）由 Service 查好傳進來。
    //  好處是這些方法不需要 DbContext，規則一眼就看得完，也可以直接寫測試。
    //
    //  10/10：ViewModel 搬到 Dtos/ 之後，原本「借 VM 來算」的規則直接寫在這裡：
    //    - 確認區塊的兩行字（原本在 ReviewCreateForVisitVM／ReviewCreateForBookingVM 的 ContextPrimary／ContextSecondary）
    //    - 公開卡片的顯示名稱（原本在 ReviewCardVM.ResolveDisplayName）
    //    - 平均分數、百分比（原本在 StarDistributionVM）
    //    規則本身一個字都沒改，只是換了位置。
    // ════════════════════════════════════════════════════════════════
    public static class ReviewMapper
    {
        // ── 確認區塊 ─────────────────────────────────────────

        /// <summary>現場評論的確認區塊：場地名稱／「M/d HH:mm 使用」。</summary>
        public static ReviewContextDto VisitContext(string? venueName, DateTime rentStartTime)
            => new(venueName, rentStartTime.ToString("M/d HH:mm") + " 使用");

        /// <summary>
        /// 預約評論撰寫頁的確認區塊：訂單編號／付款方式。
        /// 沒有訂單編號字串時退回「訂單 #Id」。
        /// </summary>
        public static ReviewContextDto BookingContext(int orderId, string? orderNo, byte? paymentMethod)
            => new(
                orderNo ?? $"訂單 #{orderId}",
                // TODO: PaymentMethod 的代碼對照表要跟負責訂單的組員要，
                //       拿到之後把數字換成「信用卡」這類看得懂的字。
                paymentMethod == null ? null : $"付款方式代碼 {paymentMethod}");

        // ── 評論專區 ─────────────────────────────────────────

        /// <summary>
        /// 公開卡片上的顯示名稱。匿名但暱稱意外是 null 時要有替代文字，否則畫面會空白。
        /// </summary>
        public static string PublicDisplayName(bool isAnonymous, string? anonymousNickname, string? memberName)
            => isAnonymous
                ? (anonymousNickname ?? "匿名使用者")
                : (memberName ?? "使用者");

        /// <summary>
        /// 公開卡片。⚠️ 不帶 ReviewId。
        /// venueName、memberName 由呼叫端一次查好（不要在這裡一則一則查資料庫）。
        /// </summary>
        public static PublicReviewCardDto ToPublicCard(ReviewMain r, string? venueName, string? memberName) => new(
            StarRating:    r.StarRating,
            Content:       r.ReviewContent == null ? null : ReviewTextGuard.MaskForPublic(r.ReviewContent),   // 公開時一律遮蔽個資、連結、不雅字詞
            IsRatingOnly:  string.IsNullOrWhiteSpace(r.ReviewContent),
            CreatedAt:     TimeView.From(r.CreatedAt),
            MentionsVenue: r.MentionsVenue,
            MentionsStaff: r.MentionsStaff,
            VenueName:     venueName,
            DisplayName:   PublicDisplayName(r.IsAnonymous, r.AnonymousNickname, memberName),
            Reply:         r.RepliedAt is DateTime at
                               ? new PublicReplyDto(r.ReplyContent ?? "", TimeView.From(at))
                               : null);

        /// <summary>
        /// 星等分布 → 平均分數、總則數、五條長條（5 星在最上面）。
        /// ⚠️ 總數是 0 時平均和百分比都回 0——新場館或篩選後沒有資料，不能除以零。
        /// </summary>
        /// <param name="countByStar">幾顆星 → 幾則。沒有出現的星等當成 0。</param>
        public static RatingSummaryDto ToRatingSummary(IReadOnlyDictionary<byte, int> countByStar)
        {
            int CountOf(int star) => countByStar.GetValueOrDefault((byte)star);

            int total = 0, weighted = 0;
            for (int s = 1; s <= 5; s++)
            {
                total += CountOf(s);
                weighted += CountOf(s) * s;
            }

            double average = total == 0 ? 0 : (double)weighted / total;
            var bars = new[] { 5, 4, 3, 2, 1 }
                .Select(s => new StarBarDto(s, CountOf(s), total == 0 ? 0 : CountOf(s) * 100 / total))
                .ToList();

            return new RatingSummaryDto(average, total, bars);
        }

        // ── 我的評論 ─────────────────────────────────────────

        /// <summary>「我的評論」（規格 2-4），員工預覽（2-8）也用同一個。</summary>
        public static MyReviewDto ToMyReview(ReviewMain review, string kind, ReviewContextDto context, string displayName)
        {
            MyReplyDto? reply = review.RepliedAt is DateTime repliedAt
                ? new MyReplyDto(
                    Content: review.ReplyContent ?? "",
                    RepliedAt: TimeView.From(repliedAt),
                    IsViewed: review.ReplyViewedAt != null,
                    Satisfaction: review.ReplySatisfaction,
                    CanRateSatisfaction: review.ReplySatisfaction == null)
                : null;

            bool isSpam = review.SpamMarkedAt != null;

            // 作者看到的是公開版本（遮蔽後），跟其他顧客看到的一樣。
            // 原文仍然完整存在資料庫裡，員工端需要時可以看原文。
            string? masked = review.ReviewContent == null ? null : ReviewTextGuard.MaskForPublic(review.ReviewContent);

            return new MyReviewDto(
                Kind: kind,
                Context: context,
                DisplayName: displayName,
                IsAnonymous: review.IsAnonymous,
                StarRating: review.StarRating,
                Content: masked,
                ContentMasked: masked != review.ReviewContent,
                IsRatingOnly: string.IsNullOrWhiteSpace(review.ReviewContent),
                MentionsVenue: review.MentionsVenue,
                MentionsStaff: review.MentionsStaff,
                CreatedAt: TimeView.From(review.CreatedAt),
                IsPublic: review.IsPublic,
                IsSpamMarked: isSpam,
                SpamReasonText: isSpam ? CustomerSpamReason(review.SpamReason) : null,
                // 非垃圾 且 是現場評論（預約評論一律不公開）
                CanToggleVisibility: !isSpam && kind == ReviewKind.Visit,
                Reply: reply);
        }

        /// <summary>
        /// 下架理由的「顧客版」文字。
        /// 只說評論含有什麼，不引用被判定有問題的字句，也不指控顧客做了什麼。
        /// </summary>
        private static string CustomerSpamReason(byte? reason) =>
            reason is byte b && b < ReviewPolicy.SpamReasonInfos.Length
                ? ReviewPolicy.SpamReasonInfos[b].CustomerText
                : "評論未通過審核";   // 例外：資料庫裡的值超出清單範圍（理論上有條件約束擋著，不會發生）
    }
}
