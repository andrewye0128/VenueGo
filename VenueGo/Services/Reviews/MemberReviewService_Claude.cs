using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Dtos.ReviewDtos;
using VenueGo.Dtos.ReviewDtos.MyReviews;
using VenueGo.Dtos.ReviewDtos.WriteReview;
using VenueGo.Mappers;
using VenueGo.Models.Entities;
using VenueGo.Enums;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.ReviewScreening;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// <see cref="IMemberReviewService"/> 的實作。
    /// 10/10 從 CReviewApiController 搬過來，規則一個字都沒改；
    /// 改的只有「回傳方式」：以前直接回 404／409，現在回 <see cref="MemberReviewOutcome"/>，由 Controller 翻成 HTTP 回應。
    /// <para>
    /// 10/15 Reviews 重構：評論存在 Reviews 一張表；現場評論改用訂單 Id 找（以前是憑證 Id）；
    /// 畫面要的周邊資料改從檢視表 v_ReviewDetail 讀，查不到的欄位顯示「⋯載入失敗」，評論本身照樣列出。
    /// </para>
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
        //  10/15 Reviews 重構：現場評論的網址參數從憑證 Id 改成訂單 Id，跟預約評論一樣。
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<WriteFormDto>> GetVisitFormAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckVisitAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return MemberReviewResult<WriteFormDto>.Fail(ToOutcome(r.State));

            var slots = await VisitSlotsAsync([orderId]);

            // 10/9 起一定是會員本人：匿名改成自己選（以前沒登入時會鎖定匿名）
            return MemberReviewResult<WriteFormDto>.Ok(new WriteFormDto(
                Kind: ReviewKind.Visit,
                Context: VisitContextOf(slots, orderId),
                Form: new WriteFormOptionsDto(
                    ShowMentions: true,
                    ShowAnonymous: true,
                    CanChooseAnonymous: true,
                    ShowPublic: true,
                    Initial: new WriteFormInitialDto(IsAnonymous: false, IsPublic: true),
                    ContentMaxLength: ContentMaxLength,
                    PublicBufferDays: ReviewPolicy.PublicBufferDays)));
        }

        /// <inheritdoc/>
        public async Task<MemberReviewResult<WriteFormDto>> GetBookingFormAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return MemberReviewResult<WriteFormDto>.Fail(ToOutcome(r.State));

            string? orderNo = await OrderNoAsync(orderId);
            var channels = await PaymentChannelsAsync([orderId]);   // 10/12：付款方式改從 Payments.PaymentChannel 取

            // 預約評論不公開、不匿名；提及標籤照當初的 CreateForBooking.cshtml，先不顯示
            return MemberReviewResult<WriteFormDto>.Ok(new WriteFormDto(
                Kind: ReviewKind.Booking,
                Context: ReviewMapper.BookingContext(orderId, orderNo, ChannelOf(channels, orderId)),
                Form: new WriteFormOptionsDto(
                    ShowMentions: false,
                    ShowAnonymous: false,
                    CanChooseAnonymous: false,
                    ShowPublic: false,
                    Initial: new WriteFormInitialDto(IsAnonymous: false, IsPublic: false),
                    ContentMaxLength: ContentMaxLength,
                    PublicBufferDays: ReviewPolicy.PublicBufferDays)));
        }

        // ════════════════════════════════════════════════════════
        //  送出評論（規格 2-3）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewOutcome> CreateVisitReviewAsync(int orderId, int? userId, ReviewCreateForVisitDto input)
        {
            // ⚠️ 資格要重驗一次：表單可能停在畫面上好幾天，也可能有人繞過畫面直接送請求
            var r = await _eligibility.CheckVisitAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return ToOutcome(r.State);

            // 匿名照會員自己的選擇。TicketId 存「判斷資格用的那張票」，檢視表靠它查到館時間
            var review = BuildNewReview(input, r.Target!, input.IsAnonymous, input.IsPublic);
            return await SaveNewReviewAsync(review);
        }

        /// <inheritdoc/>
        public async Task<MemberReviewOutcome> CreateBookingReviewAsync(int orderId, int? userId, ReviewCreateForBookingDto input)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            if (r.State != ReviewEligibilityState.Ok)
                return ToOutcome(r.State);

            // 預約評論一律不公開、不匿名（DTO 上也沒有這兩個欄位；資料表的 CHK_Reviews_KindFields 也擋著）
            var review = BuildNewReview(input, r.Target!, isAnonymous: false, isPublic: false);
            return await SaveNewReviewAsync(review);
        }

        // ════════════════════════════════════════════════════════
        //  查看（規格 2-4、2-8），純讀取、不寫入已讀
        //  10/15：畫面要的周邊資料（場地、時段、訂單編號）一律從 v_ReviewDetail 讀，查不到的顯示「載入失敗」。
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetVisitReviewAsync(int orderId, int? userId)
            => await GetOwnReviewAsync(await _eligibility.CheckVisitAsync(orderId, userId));

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetBookingReviewAsync(int orderId, int? userId)
            => await GetOwnReviewAsync(await _eligibility.CheckBookingAsync(orderId, userId));

        /// <inheritdoc/>
        public async Task<MemberReviewResult<MyReviewDto>> GetPreviewAsync(int reviewId)
        {
            // 10/12：時間在未來的評論（時光機撥回來之後）當成查不到，員工預覽也一樣
            var detail = await _db.VReviewDetails
                                  .Where(ReviewTimeline.DetailHappenedBy(_timeService.Now))
                                  .FirstOrDefaultAsync(d => d.ReviewId == reviewId);
            return detail == null
                ? MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound)
                : MemberReviewResult<MyReviewDto>.Ok(ToMyReview(detail, _timeService.Now));   // 10/27：多傳現在時間
        }

        private async Task<MemberReviewResult<MyReviewDto>> GetOwnReviewAsync(ReviewEligibility r)
        {
            if (r.State != ReviewEligibilityState.AlreadyReviewed)
                return MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound);

            int reviewId = r.ExistingReview!.ReviewId;
            var detail = await _db.VReviewDetails.FirstOrDefaultAsync(d => d.ReviewId == reviewId);
            return detail == null   // 理論上不會：檢視表對 Reviews 全部 LEFT JOIN，Reviews 有就一定有
                ? MemberReviewResult<MyReviewDto>.Fail(MemberReviewOutcome.NotFound)
                : MemberReviewResult<MyReviewDto>.Ok(ToMyReview(detail, _timeService.Now));   // 10/27：多傳現在時間
        }

        // ════════════════════════════════════════════════════════
        //  我的評論清單（10/10，規格 2-9、2-10）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<IReadOnlyList<PendingReviewItemDto>> ListPendingAsync(int? userId)
        {
            if (userId is not int uid) return [];

            var targets = await _eligibility.ListPendingAsync(uid);   // 已經照期限排好
            DateTime now = _timeService.Now;

            var visitOrderIds = targets.Where(t => t.Kind == ReviewKindCode.Visit).Select(t => t.OrderId).ToList();
            var bookingOrderIds = targets.Where(t => t.Kind == ReviewKindCode.Booking).Select(t => t.OrderId).ToList();

            var slots = await VisitSlotsAsync(visitOrderIds);
            var orderNos = await OrderNosAsync(bookingOrderIds);
            var channels = await PaymentChannelsAsync(bookingOrderIds);   // 10/12

            // 預約評論的兩行字跟撰寫頁一樣（訂單編號／付款方式），會員點進去看到的確認區塊才對得上
            return targets.Select(t => t.Kind == ReviewKindCode.Visit
                               ? new PendingReviewItemDto(
                                     Kind: ReviewKind.Visit,
                                     // 10/25 改名 OrderId，原本：Ticket: t.OrderId,
                                     OrderId: t.OrderId,
                                     Context: VisitContextOf(slots, t.OrderId),
                                     Deadline: ReviewMapper.ToDeadline(t.ExpiredAt, now))
                               : new PendingReviewItemDto(
                                     Kind: ReviewKind.Booking,
                                     // 10/25 改名 OrderId，原本：Ticket: t.OrderId,
                                     OrderId: t.OrderId,
                                     Context: ReviewMapper.BookingContext(t.OrderId, orderNos.GetValueOrDefault(t.OrderId), ChannelOf(channels, t.OrderId)),
                                     Deadline: ReviewMapper.ToDeadline(t.ExpiredAt, now)))
                          .ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewedItemDto>> ListReviewedAsync(int? userId)
        {
            if (userId is not int uid) return [];

            // 10/15：從 v_ReviewDetail 讀（LEFT JOIN，周邊資料查不到也照樣列出，清單一定完整）。
            // 只選清單用得到的欄位（不讀館方回覆全文、員工欄位）。
            // 評論內容要整段讀：先遮蔽再截斷，截斷後才遮蔽的話，切在一半的電話號碼會漏網。
            // 10/12：時間在未來的評論不列出來（ReviewTimeline）
            var rows = await _db.VReviewDetails
                                .Where(ReviewTimeline.DetailHappenedBy(_timeService.Now))
                                .Where(d => d.UserId == uid)
                                .OrderByDescending(d => d.CreatedAt)
                                .Select(d => new
                                {
                                    d.ReviewKind, d.OrderId, d.OrderNo, d.VenueName, d.RentStartTime,
                                    d.StarRating, d.ReviewContent, d.CreatedAt, d.IsPublic,
                                    d.SpamMarkedAt, d.RepliedAt, d.ReplyViewedAt
                                })
                                .ToListAsync();

            // ⚠️ 還沒有分頁：一位會員的評論數量有限（每筆訂單最多各一則），之後真的變多再補 Skip／Take。
            return rows.Select(d => new ReviewedItemDto(
                           Kind: ReviewKindCodeExtensions.ToRouteKind(d.ReviewKind),
                           // 10/25 改名 OrderId，原本：Ticket: d.OrderId,
                           OrderId: d.OrderId,
                           Context: d.ReviewKind == (byte)ReviewKindCode.Visit
                                        ? ReviewMapper.VisitContext(d.VenueName, d.RentStartTime)
                                        : BookingReviewContext(d.OrderId, d.OrderNo),
                           StarRating: d.StarRating,
                           Excerpt: ReviewMapper.ToExcerpt(d.ReviewContent),
                           CreatedAt: TimeView.From(d.CreatedAt),
                           IsPublic: d.IsPublic,
                           IsSpamMarked: d.SpamMarkedAt != null,
                           HasReply: d.RepliedAt != null,
                           HasUnviewedReply: d.RepliedAt != null && d.ReplyViewedAt == null))
                       .ToList();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ReviewStatusItemDto>> ListReviewStatusesAsync(int? userId)
        {
            if (userId is not int uid) return [];

            // 「立刻評論」：待評論清單的對象（同一套規則）
            var pending = await _eligibility.ListPendingAsync(uid);

            // 「查看評論」：已經寫過、而且已經發生的評論（同已評論清單）
            var reviewed = await _db.Reviews
                                    .Where(ReviewTimeline.ReviewHappenedBy(_timeService.Now))
                                    .Where(r => r.UserId == uid)
                                    .Select(r => new { r.OrderId, r.ReviewKind, r.TicketId })
                                    .ToListAsync();

            var orderIds = pending.Select(t => t.OrderId).Concat(reviewed.Select(r => r.OrderId)).Distinct().ToList();
            if (orderIds.Count == 0) return [];

            var reservationIds = await _db.Orders
                                          .Where(o => orderIds.Contains(o.OrderId))
                                          .ToDictionaryAsync(o => o.OrderId, o => o.ReservationId);

            return orderIds.Select(orderId =>
            {
                var visitDone = reviewed.FirstOrDefault(r => r.OrderId == orderId && r.ReviewKind == (byte)ReviewKindCode.Visit);
                var visitTodo = pending.FirstOrDefault(t => t.OrderId == orderId && t.Kind == ReviewKindCode.Visit);
                bool bookingDone = reviewed.Any(r => r.OrderId == orderId && r.ReviewKind == (byte)ReviewKindCode.Booking);
                bool bookingTodo = pending.Any(t => t.OrderId == orderId && t.Kind == ReviewKindCode.Booking);

                return new ReviewStatusItemDto(
                    OrderId: orderId,
                    ReservationId: reservationIds.GetValueOrDefault(orderId),
                    VisitAction: visitDone != null ? ReviewStatusAction.View : visitTodo != null ? ReviewStatusAction.Write : null,
                    VisitTicketId: visitDone?.TicketId ?? visitTodo?.TicketId,
                    BookingAction: bookingDone ? ReviewStatusAction.View : bookingTodo ? ReviewStatusAction.Write : null);
            }).ToList();
        }

        /// <summary>
        /// 每筆訂單「用什麼付的」（10/12）：取最早一筆已付款紀錄的 PaymentChannel，跟資格的期限用同一筆付款。
        /// 一筆訂單的付款紀錄很少，所以整批讀回來再分組，不在 SQL 裡寫「每組取第一筆」。
        /// </summary>
        private async Task<Dictionary<int, PaymentChannel>> PaymentChannelsAsync(IEnumerable<int> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0) return [];

            var rows = await _db.Payments
                                .Where(p => ids.Contains(p.OrderId) && p.PaidAt != null)
                                .Select(p => new { p.OrderId, p.PaidAt, p.PaymentId, p.PaymentChannel })
                                .ToListAsync();

            return rows.GroupBy(p => p.OrderId)
                       .ToDictionary(g => g.Key,
                                     g => (PaymentChannel)g.OrderBy(p => p.PaidAt).ThenBy(p => p.PaymentId).First().PaymentChannel);
        }

        /// <summary>
        /// 從上面的對照表取值。查不到要回 null：直接用 GetValueOrDefault 的話，
        /// 查不到會回列舉的預設值 0（現場櫃台），畫面就會顯示一個錯的付款方式。
        /// </summary>
        private static PaymentChannel? ChannelOf(Dictionary<int, PaymentChannel> channels, int orderId)
            => channels.TryGetValue(orderId, out var channel) ? channel : null;

        /// <summary>訂單 → 場地名稱與預約開始時間（現場評論的確認區塊用）。一次查完，不要一筆一筆查。</summary>
        private async Task<Dictionary<int, (string VenueName, DateTime RentStart)>> VisitSlotsAsync(IEnumerable<int> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0) return [];

            var rows = await (from o in _db.Orders
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              join v in _db.Venues on r.VenueId equals v.VenueId
                              where ids.Contains(o.OrderId)
                              select new { o.OrderId, v.VenueName, r.BookingDate, r.StartTime })
                             .ToListAsync();

            return rows.ToDictionary(x => x.OrderId, x => (x.VenueName, x.BookingDate.ToDateTime(x.StartTime)));
        }

        /// <summary>現場評論的確認區塊。查不到場地或時段時，由 Mapper 顯示「⋯載入失敗」。</summary>
        private static ReviewContextDto VisitContextOf(Dictionary<int, (string VenueName, DateTime RentStart)> slots, int orderId)
            => slots.TryGetValue(orderId, out var s)
                ? ReviewMapper.VisitContext(s.VenueName, s.RentStart)
                : ReviewMapper.VisitContext(null, null);

        private async Task<Dictionary<int, string>> OrderNosAsync(IEnumerable<int> orderIds)
        {
            var ids = orderIds.Distinct().ToList();
            if (ids.Count == 0) return [];
            return await _db.Orders.Where(o => ids.Contains(o.OrderId)).ToDictionaryAsync(o => o.OrderId, o => o.OrderNo);
        }

        // ════════════════════════════════════════════════════════
        //  已讀、切換公開、表態（規格 2-5 ～ 2-7）
        // ════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<MemberReviewResult<ReplyViewedDto>> MarkVisitReplyViewedAsync(int orderId, int? userId)
        {
            var review = await FindOwnVisitReviewAsync(orderId, userId);
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
            int orderId, int? userId, bool isPublic)
        {
            var review = await FindOwnVisitReviewAsync(orderId, userId);
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
            int orderId, int? userId, byte satisfaction)
            => await SetSatisfactionAsync(await FindOwnVisitReviewAsync(orderId, userId), satisfaction);

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

        /// <summary>本人已經寫過的現場評論；沒有（或不是本人、或時間在未來）回 null。</summary>
        private async Task<Review?> FindOwnVisitReviewAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckVisitAsync(orderId, userId);
            return r.State == ReviewEligibilityState.AlreadyReviewed ? r.ExistingReview : null;
        }

        /// <summary>本人已經寫過的預約評論；沒有（或不是本人、或時間在未來）回 null。</summary>
        private async Task<Review?> FindOwnBookingReviewAsync(int orderId, int? userId)
        {
            var r = await _eligibility.CheckBookingAsync(orderId, userId);
            return r.State == ReviewEligibilityState.AlreadyReviewed ? r.ExistingReview : null;
        }

        private Task<string?> OrderNoAsync(int orderId)
            => _db.Orders.Where(o => o.OrderId == orderId).Select(o => (string?)o.OrderNo).FirstOrDefaultAsync();

        /// <summary>
        /// v_ReviewDetail 的一列 → 我的評論單筆頁（員工預覽也用這個）。
        /// 顯示名稱：匿名用暱稱；實名但會員資料查不到時顯示「已停用的帳號」。
        /// </summary>
        // 10/27：多了 now（公開開關旁顯示「尚未公開／已公開」用）
        private static MyReviewDto ToMyReview(VReviewDetail d, DateTime now)
        {
            var context = d.ReviewKind == (byte)ReviewKindCode.Visit
                ? ReviewMapper.VisitContext(d.VenueName, d.RentStartTime)
                : BookingReviewContext(d.OrderId, d.OrderNo);

            string displayName = d.IsAnonymous
                ? d.AnonymousNickname ?? "匿名使用者"
                : d.MemberName ?? "已停用的帳號";

            return ReviewMapper.ToMyReview(d, context, displayName, now);
        }

        /// <summary>
        /// 已經寫好的預約評論的確認區塊：只有訂單編號（我的評論單筆頁、已評論清單共用）。
        /// 訂單查不到時退回「訂單 #Id」，跟撰寫頁一樣。
        /// </summary>
        private static ReviewContextDto BookingReviewContext(int orderId, string? orderNo)
            => new(orderNo ?? $"訂單 #{orderId}", null);

        /// <summary>由輸入和評論對象組出要存的評論。</summary>
        private Review BuildNewReview(ReviewCreateInputDto input, ReviewTarget target, bool isAnonymous, bool isPublic)
            => new()
            {
                ReviewKind = (byte)target.Kind,
                UserId = target.UserId,
                OrderId = target.OrderId,
                TicketId = target.TicketId,                        // 預約評論是 null
                StarRating = input.StarRating!.Value,              // [Required] 已保證不為 null
                ReviewContent = string.IsNullOrWhiteSpace(input.ReviewContent)
                                  ? null                           // 純空白存 null，否則撞 Content_NotBlank
                                  : input.ReviewContent.Trim(),
                IsAnonymous = isAnonymous,
                IsPublic = isPublic,
                // 預約評論不顯示提及標籤，資料表也規定一定是 0（CHK_Reviews_KindFields）
                MentionsVenue = target.Kind == ReviewKindCode.Visit && input.MentionsVenue,
                MentionsStaff = target.Kind == ReviewKindCode.Visit && input.MentionsStaff,
                AnonymousNickname = isAnonymous ? NicknameGenerator.Generate() : null,
                CreatedAt = _timeService.Now
            };

        /// <summary>
        /// 存評論，再建立預審紀錄。
        /// 兩步「刻意不包在同一個交易裡」：預審是附加功能，AI 沒設定或出錯都不能讓顧客的評論存不進去。
        /// （CreateForReviewAsync 內部自己接住例外；沒設定金鑰時什麼都不做。）
        /// <para>
        /// 10/15：同一位會員對同一筆訂單連按兩次送出，兩個請求可能都通過資格檢查，
        /// 第二個會撞到 UQ_Reviews_User_Order_Kind。這種情況回「已評過」，不讓它變成 500。
        /// </para>
        /// </summary>
        private async Task<MemberReviewOutcome> SaveNewReviewAsync(Review review)
        {
            _db.Reviews.Add(review);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _db.Entry(review).State = EntityState.Detached;
                return MemberReviewOutcome.AlreadyReviewed;
            }

            await _screening.CreateForReviewAsync(review);
            return MemberReviewOutcome.Ok;
        }

        /// <summary>SQL Server 的唯一約束／唯一索引衝突（錯誤號碼 2627、2601）。</summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2627 or 2601;

        /// <summary>
        /// 記錄「顧客已看過回覆」。只在「有回覆且尚未記錄」時寫，重複呼叫不會蓋掉第一次的時間。
        /// 回傳這次之後「是不是已讀」。
        /// </summary>
        private async Task<bool> MarkReplyViewedIfNeededAsync(Review review)
        {
            if (review.RepliedAt == null) return false;
            if (review.ReplyViewedAt != null) return true;

            review.ReplyViewedAt = _timeService.Now;
            await _db.SaveChangesAsync();
            return true;
        }

        private async Task<MemberReviewResult<SatisfactionResultDto>> SetSatisfactionAsync(Review? review, byte satisfaction)
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
