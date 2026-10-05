using VenueGo.Dtos.ReviewDtos;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.ReviewScreening;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Mappers
{
    // ════════════════════════════════════════════════════════════════
    //  顧客端評論：資料表／ViewModel → 給前台的 DTO（9/29 從 CReviewApiController 抽出來）
    //
    //  組員開發注意事項：「給前台的 API 不要直接回傳 Entity，要轉成 Dtos/ 裡的格式，
    //  轉換的程式放在 Mappers/」。
    //
    //  這裡只做「轉換」，不查資料庫：要用到的名稱（顯示名稱、場地名稱）由 Controller 查好傳進來。
    //  好處是這些方法不需要 DbContext，規則一眼就看得完，也可以直接寫測試。
    // ════════════════════════════════════════════════════════════════
    public static class ReviewMapper
    {
        /// <summary>
        /// 現場評論的確認區塊。借用 ReviewCreateForVisitVM 的 ContextPrimary／ContextSecondary，
        /// 「M/d HH:mm 使用」這個格式只寫在 VM 那一個地方。
        /// </summary>
        public static ReviewContextDto VisitContext(string? venueName, DateTime rentStartTime)
        {
            var vm = new ReviewCreateForVisitVM { VenueName = venueName, RentStartTime = rentStartTime };
            return new(vm.ContextPrimary, vm.ContextSecondary);
        }

        /// <summary>預約評論的確認區塊。訂單編號、付款方式的顯示規則在 ReviewCreateForBookingVM 裡。</summary>
        public static ReviewContextDto BookingContext(int orderId, string? orderNo, byte? paymentMethod)
        {
            var vm = new ReviewCreateForBookingVM { OrderId = orderId, OrderNo = orderNo, PaymentMethod = paymentMethod };
            return new(vm.ContextPrimary, vm.ContextSecondary);
        }

        /// <summary>
        /// 公開卡片。先組成既有的 ReviewCardVM（顯示名稱、IsRatingOnly 的規則都在 VM 裡），再轉成 DTO。
        /// 轉的時候丟掉 ReviewId——VM 有、API 不給。
        /// </summary>
        public static PublicReviewCardDto ToPublicCard(ReviewCardVM card) => new(
            StarRating:    card.StarRating,
            Content:       card.Content == null ? null : ReviewTextGuard.MaskForPublic(card.Content),   // 公開時一律遮蔽個資、連結、不雅字詞
            IsRatingOnly:  card.IsRatingOnly,
            CreatedAt:     TimeView.From(card.CreatedAt),
            MentionsVenue: card.MentionsVenue,
            MentionsStaff: card.MentionsStaff,
            VenueName:     card.VenueName,
            DisplayName:   card.DisplayName,
            Reply:         card.RepliedAt is DateTime at
                               ? new PublicReplyDto(card.ReplyContent ?? "", TimeView.From(at))
                               : null);

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
                // 同 MyReviewPageVM.CanToggleVisibility：非垃圾 且 是現場評論
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
