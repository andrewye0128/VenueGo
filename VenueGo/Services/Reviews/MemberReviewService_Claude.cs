using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using VenueGo.Data;
using VenueGo.Dtos.ReviewDtos;
using VenueGo.Dtos.ReviewDtos.MyReviews;
using VenueGo.Dtos.ReviewDtos.WriteReview;
using VenueGo.Helpers;
using VenueGo.Mappers;
using VenueGo.Models.Entities;
using VenueGo.Services.ReviewScreening;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// <see cref="IMemberReviewService"/> 的實作。
    /// 10/10 從 CReviewApiController 搬過來，規則一個字都沒改；
    /// 改的只有「回傳方式」：以前直接回 404／409，現在回 <see cref="MemberReviewOutcome"/>，由 Controller 翻成 HTTP 回應。
    /// </summary>
    public sealed class MemberReviewService(
        dbVenueContext db,
        IReviewEligibilityService eligibility,
        ITimeService timeService,
        IReviewScreeningService screening) : IMemberReviewService
    {
        private readonly dbVenueContext _db = db;
        private readonly IReviewEligibilityService _eligibility = eligibility;
        private readonly ITimeService _timeService = timeService;
        private readonly IReviewScreeningService _screening = screening;

        /// <summary>
        /// 評論內容的字數上限，直接讀 ReviewCreateInputDto 上的 [StringLength(1000)]。
        ///
        /// 為什麼不寫死 1000：這個數字已經寫在 DTO 的驗證屬性上了，
        /// 這裡再寫一次就是兩個地方各記一份，改了一邊忘了另一邊，
        /// 前端顯示的上限就會跟後端擋的上限對不起來。
        /// static readonly 只在第一次用到時讀一次，之後不再讀。
        /// </summary>
        private static readonly int ContentMaxLength =
            typeof(ReviewCreateInputDto)
                .GetProperty(nameof(ReviewCreateInputDto.ReviewContent))!
                .GetCustomAttribute<StringLengthAttribute>()!
                .MaximumLength;

        // ════════════════════════════════════════════════════════
        //  撰寫頁設定（規格 2-2）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<WriteFormDto>> GetVisitFormAsync(int reviewPerVisitId, int? userId)
        {
            var r = await _eligibility.CheckVisitAsync(reviewPerVisitId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return MemberReviewResult<WriteFormDto>.Fail(ToOutcome(r.State));

            var ticket = r.Ticket!;
            string? venueName = await VenueNameAsync(ticket.VenueId);

            // 10/9 起一定是會員本人：匿名改成自己選（以前沒登入時會鎖定匿名）
            return MemberReviewResult<WriteFormDto>.Ok(new WriteFormDto(
                Kind: ReviewKind.Visit,
                Context: ReviewMapper.VisitContext(venueName, ticket.RentStartTime),
                Form: new WriteFormOptionsDto(
                    ShowMentions: true,
                    ShowAnonymous: true,
                    CanChooseAnonymous: true,
                    ShowPublic: true,
                    Initial: new WriteFormInitialDto(IsAnonymous: false, IsPublic: true),
                    ContentMaxLength: ContentMaxLength)));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<WriteFormDto>> GetBookingFormAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return MemberReviewResult<WriteFormDto>.Fail(ToOutcome(r.State));

            var ticket = r.Ticket!;
            string? orderNo = await OrderNoAsync(ticket.OrderId);

            // 預約評論不公開、不匿名；提及標籤照當初的 CreateForBooking.cshtml，先不顯示
            return MemberReviewResult<WriteFormDto>.Ok(new WriteFormDto(
                Kind: ReviewKind.Booking,
                Context: ReviewMapper.BookingContext(ticket.OrderId, orderNo, ticket.PaymentMethod),
                Form: new WriteFormOptionsDto(
                    ShowMentions: false,
                    ShowAnonymous: false,
                    CanChooseAnonymous: false,
                    ShowPublic: false,
                    Initial: new WriteFormInitialDto(IsAnonymous: false, IsPublic: false),
                    ContentMaxLength: ContentMaxLength)));
        }

        // ════════════════════════════════════════════════════════
        //  送出評論（規格 2-3）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewOutcome> CreateVisitReviewAsync(
            int reviewPerVisitId, int? userId, ReviewCreateForVisitDto input)
        {
            // ⚠️ 資格要重驗一次：表單可能停在畫面上好幾天，也可能有人繞過畫面直接送請求
            var r = await _eligibility.CheckVisitAsync(reviewPerVisitId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return ToOutcome(r.State);

            // 10/9 起一定是會員本人（資格判定已經比對過憑證上的 UserId），匿名照會員自己的選擇
            var review = BuildNewReview(input, input.IsAnonymous, input.IsPublic,
                                        visitId: r.Ticket!.ReviewPerVisitId, bookingId: null, userId);
            await SaveNewReviewAsync(review);
            return MemberReviewOutcome.Ok;
        }

        /// <inheritdoc/>
        public async Task<MemberReviewOutcome> CreateBookingReviewAsync(
            int orderId, int? userId, ReviewCreateForBookingDto input)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return ToOutcome(r.State);

            // 預約評論一律不公開、不匿名（DTO 上也沒有這兩個欄位）
            var review = BuildNewReview(input, isAnonymous: false, isPublic: false,
                                        visitId: null, bookingId: r.Ticket!.ReviewPerBookingId, userId);
            await SaveNewReviewAsync(review);
            return MemberReviewOutcome.Ok;
        }

        // ════════════════════════════════════════════════════════
        //  查看（規格 2-4、2-8），純讀取、不寫入已讀
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetVisitReviewAsync(int reviewPerVisitId, int? userId)
        {
            var r = await _eligibility.CheckVisitAsync(reviewPerVisitId, userId);
            if (r.State != ReviewEligibilityState.AlreadyReviewed)
                return MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound);

            return MemberReviewResult<MyReviewDto>.Ok(await BuildVisitReviewAsync(r.ExistingReview!, r.Ticket!));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetBookingReviewAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.AlreadyReviewed)
                return MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound);

            return MemberReviewResult<MyReviewDto>.Ok(await BuildBookingReviewAsync(r.ExistingReview!, r.Ticket!));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetPreviewAsync(int reviewId)
        {
            var notFound = MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound);

            var review = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
            if (review == null) return notFound;

            if (review.ReviewPerVisitId is int visitId)
            {
                var ticket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.ReviewPerVisitId == visitId);
                return ticket == null ? notFound
                                      : MemberReviewResult<MyReviewDto>.Ok(await BuildVisitReviewAsync(review, ticket));
            }

            if (review.ReviewPerBookingId is int bookingId)
            {
                var ticket = await _db.ReviewPerBookings.FirstOrDefaultAsync(b => b.ReviewPerBookingId == bookingId);
                return ticket == null ? notFound
                                      : MemberReviewResult<MyReviewDto>.Ok(await BuildBookingReviewAsync(review, ticket));
            }

            // 資料庫的 XOR 約束保證兩個一定剛好有一個，理論上到不了這裡。
            // 真的到了代表資料異常，當成查不到，不要回一個種類不明的畫面。
            return notFound;
        }

        // ════════════════════════════════════════════════════════
        //  已讀、切換公開、表態（規格 2-5 ～ 2-7）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<ReplyViewedDto>> MarkVisitReplyViewedAsync(int reviewPerVisitId, int? userId)
        {
            var review = await FindOwnVisitReviewAsync(reviewPerVisitId, userId);
            return review == null
                ? MemberReviewResult<ReplyViewedDto>.Fail(MemberReviewOutcome.NotFound)
                : MemberReviewResult<ReplyViewedDto>.Ok(new ReplyViewedDto(await MarkReplyViewedIfNeededAsync(review)));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<ReplyViewedDto>> MarkBookingReplyViewedAsync(int orderId, int? userId)
        {
            var review = await FindOwnBookingReviewAsync(orderId, userId);
            return review == null
                ? MemberReviewResult<ReplyViewedDto>.Fail(MemberReviewOutcome.NotFound)
                : MemberReviewResult<ReplyViewedDto>.Ok(new ReplyViewedDto(await MarkReplyViewedIfNeededAsync(review)));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<VisibilityResultDto>> SetVisitVisibilityAsync(
            int reviewPerVisitId, int? userId, bool isPublic)
        {
            var review = await FindOwnVisitReviewAsync(reviewPerVisitId, userId);
            if (review == null)
                return MemberReviewResult<VisibilityResultDto>.Fail(MemberReviewOutcome.NotFound);

            // 被標記垃圾的評論強制不公開
            if (review.SpamMarkedAt != null)
                return MemberReviewResult<VisibilityResultDto>.Fail(MemberReviewOutcome.SpamMarked);

            review.IsPublic = isPublic;
            await _db.SaveChangesAsync();

            return MemberReviewResult<VisibilityResultDto>.Ok(new VisibilityResultDto(review.IsPublic));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<SatisfactionResultDto>> SetVisitSatisfactionAsync(
            int reviewPerVisitId, int? userId, byte satisfaction)
            => await SetSatisfactionAsync(await FindOwnVisitReviewAsync(reviewPerVisitId, userId), satisfaction);

        /// <inheritdoc/>
        public async Task<MemberReviewResult<SatisfactionResultDto>> SetBookingSatisfactionAsync(
            int orderId, int? userId, byte satisfaction)
            => await SetSatisfactionAsync(await FindOwnBookingReviewAsync(orderId, userId), satisfaction);

        // ════════════════════════════════════════════════════════
        //  私有方法
        // ════════════════════════════════════════════════════════

        /// <summary>資格判定的結果 → 會員操作的結果種類。</summary>
        private static MemberReviewOutcome ToOutcome(ReviewEligibilityState state) => state switch
        {
            ReviewEligibilityState.Ok              => MemberReviewOutcome.Ok,
            ReviewEligibilityState.NotFound        => MemberReviewOutcome.NotFound,
            ReviewEligibilityState.Expired         => MemberReviewOutcome.Expired,
            ReviewEligibilityState.AlreadyReviewed => MemberReviewOutcome.AlreadyReviewed,
            // 四種狀態都列在上面了。會走到這裡代表有人新增了狀態卻忘了處理，
            // 直接丟例外讓它在開發時就被看到，不要默默當成「可以評論」放行。
            _ => throw new InvalidOperationException($"未處理的資格狀態：{state}")
        };

        /// <summary>本人已經寫過的現場評論；沒有（或不是本人）回 null。</summary>
        private async Task<ReviewMain?> FindOwnVisitReviewAsync(int reviewPerVisitId, int? userId)
        {
            var r = await _eligibility.CheckVisitAsync(reviewPerVisitId, userId);
            return r.State == ReviewEligibilityState.AlreadyReviewed ? r.ExistingReview : null;
        }

        /// <summary>本人已經寫過的預約評論；沒有（或不是本人）回 null。</summary>
        private async Task<ReviewMain?> FindOwnBookingReviewAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            return r.State == ReviewEligibilityState.AlreadyReviewed ? r.ExistingReview : null;
        }

        private Task<string?> VenueNameAsync(int venueId)
            => _db.Venues.Where(v => v.VenueId == venueId).Select(v => (string?)v.VenueName).FirstOrDefaultAsync();

        private Task<string?> OrderNoAsync(int orderId)
            => _db.Orders.Where(o => o.OrderId == orderId).Select(o => (string?)o.OrderNo).FirstOrDefaultAsync();

        /// <summary>「我的評論」上的顯示名稱。實名但帳號查不到時顯示「已停用的帳號」。</summary>
        private async Task<string> ResolveDisplayNameAsync(ReviewMain review)
        {
            if (review.IsAnonymous)
                return review.AnonymousNickname ?? "匿名使用者";

            string? name = await _db.Users.Where(u => u.UserId == review.UserId)
                                          .Select(u => (string?)u.Name)
                                          .FirstOrDefaultAsync();
            return name ?? "已停用的帳號";
        }

        private async Task<MyReviewDto> BuildVisitReviewAsync(ReviewMain review, ReviewPerVisit ticket)
            => ReviewMapper.ToMyReview(review, ReviewKind.Visit,
                                       ReviewMapper.VisitContext(await VenueNameAsync(ticket.VenueId), ticket.RentStartTime),
                                       await ResolveDisplayNameAsync(review));

        /// <summary>
        /// ⚠️ 確認區塊第二行是 null（只顯示訂單編號），跟撰寫頁（會顯示付款方式）不一樣。
        /// 這是搬過來之前就這樣，刻意先不改；要統一的話改用 ReviewMapper.BookingContext。
        /// </summary>
        private async Task<MyReviewDto> BuildBookingReviewAsync(ReviewMain review, ReviewPerBooking ticket)
            => ReviewMapper.ToMyReview(review, ReviewKind.Booking,
                                       new ReviewContextDto(await OrderNoAsync(ticket.OrderId), null),
                                       await ResolveDisplayNameAsync(review));

        /// <summary>由輸入組出要存的評論。visitId 與 bookingId 一定只有一個有值（XOR 約束）。</summary>
        private ReviewMain BuildNewReview(
            ReviewCreateInputDto input, bool isAnonymous, bool isPublic, int? visitId, int? bookingId, int? userId)
            => new()
            {
                ReviewPerVisitId = visitId,
                ReviewPerBookingId = bookingId,
                UserId = userId,
                StarRating = input.StarRating!.Value,              // [Required] 已保證不為 null
                ReviewContent = string.IsNullOrWhiteSpace(input.ReviewContent)
                                  ? null                           // 純空白存 null，否則撞 Content_NotBlank
                                  : input.ReviewContent.Trim(),
                IsAnonymous = isAnonymous,
                IsPublic = isPublic,
                MentionsVenue = input.MentionsVenue,
                MentionsStaff = input.MentionsStaff,
                AnonymousNickname = isAnonymous ? NicknameGenerator.Generate() : null,
                CreatedAt = _timeService.Now
            };

        /// <summary>
        /// 存評論，再建立預審紀錄。
        /// 兩步「刻意不包在同一個交易裡」：預審是附加功能，AI 沒設定或出錯都不能讓顧客的評論存不進去。
        /// （CreateForReviewAsync 內部自己接住例外；沒設定金鑰時什麼都不做。）
        /// </summary>
        private async Task SaveNewReviewAsync(ReviewMain review)
        {
            _db.ReviewMains.Add(review);
            await _db.SaveChangesAsync();
            await _screening.CreateForReviewAsync(review);
        }

        /// <summary>
        /// 記錄「顧客已看過回覆」。只在「有回覆且尚未記錄」時寫，重複呼叫不會蓋掉第一次的時間。
        /// 回傳這次之後「是不是已讀」。
        /// </summary>
        private async Task<bool> MarkReplyViewedIfNeededAsync(ReviewMain review)
        {
            if (review.RepliedAt == null) return false;
            if (review.ReplyViewedAt != null) return true;

            review.ReplyViewedAt = _timeService.Now;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<MemberReviewResult<SatisfactionResultDto>> SetSatisfactionAsync(ReviewMain? review, byte satisfaction)
        {
            if (review == null)
                return MemberReviewResult<SatisfactionResultDto>.Fail(MemberReviewOutcome.NotFound);
            if (review.RepliedAt == null)
                return MemberReviewResult<SatisfactionResultDto>.Fail(MemberReviewOutcome.NoReply);
            if (review.ReplySatisfaction != null)
                return MemberReviewResult<SatisfactionResultDto>.Fail(MemberReviewOutcome.AlreadyRated);

            review.ReplyViewedAt ??= _timeService.Now;   // 保險：約束要求表態前一定已讀
            review.ReplySatisfaction = satisfaction;
            await _db.SaveChangesAsync();

            return MemberReviewResult<SatisfactionResultDto>.Ok(new SatisfactionResultDto(satisfaction));
        }
    }
}
