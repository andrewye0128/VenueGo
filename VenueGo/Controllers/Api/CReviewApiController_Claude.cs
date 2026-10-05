using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using VenueGo.Data;
using VenueGo.Dtos.ReviewDtos;
using VenueGo.Helpers;
using VenueGo.Mappers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.Services.ReviewTickets;
using VenueGo.Services.ReviewScreening;
using VenueGo.ViewModels;
using VenueGo.ViewModels.ReviewVM;
using VenueGo.Models.ReviewModels;

namespace VenueGo.Controllers.Api
{
    // ════════════════════════════════════════════════════════════════
    //  顧客端評論 API（取代 Controllers/CReviewController.cs 的 Razor 版）
    //  規格：《API規格_CReview.md》。這支的每個 Action 標題都寫了對應的規格編號。
    //
    //  ── 為什麼叫 CReviewApiController，不沿用 CReviewController ─────────
    //  過渡期兩支會同時存在（Vue 還沒上線前，Razor 版還要能用）。
    //  就算放在不同命名空間，兩個同名的 Controller 在「預設路由」
    //  （{controller}/{action}）下會變成「/CReview/Index 該找哪一支」的衝突。
    //  換個名字就沒有這個問題。等 Razor 版刪掉之後想改回來，只要改類別名稱，
    //  網址不受影響（網址是下面 [Route] 決定的，跟類別名稱無關）。
    //
    //  ── 為什麼繼承 ControllerBase，不是 Controller ────────────────────
    //  Controller = ControllerBase + View 相關的功能（View()、TempData、ViewBag⋯）。
    //  這支只回 JSON，用不到那些。繼承 ControllerBase，想不小心寫出 return View() 也寫不出來。
    //
    //  ── [ApiController]（9/29 起照組裡規定掛上）─────────────────────
    //  掛了之後，模型驗證失敗會在「進 Action 之前」就被框架攔下（回 400）。
    //  回應的格式由 Program.cs 掛的 ApiResponses.InvalidModelState 統一成 ApiResult（規格 0-5），
    //  所以這支不再自己檢查 ModelState。
    //  ⚠️ 附帶的順序變化：表單有錯「而且」憑證也過期時，以前先回 410，現在先回 400。
    //     前台兩種都會顯示訊息，不影響使用。
    //
    //  ── 位置（9/29）──────────────────────────────────────────────
    //  組裡規定 API 放 Controllers/Api/；回應格式放 Dtos/ReviewScreening/；轉換放 Mappers/ReviewMapper。
    //
    //  ── 跟 Razor 版的邏輯關係 ────────────────────────────────────────
    //  資格判定、上架條件、組卡片、寫入的規則，全部照搬 CReviewController，沒有改規則。
    //  改的只有「怎麼回應」：轉址＋TempData → 狀態碼＋JSON。
    //  ⚠️ 過渡期兩邊各有一份，改規則時兩邊都要改。Razor 版刪掉之後就只剩這一份。
    // ════════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/reviews")]
    [AutoValidateAntiforgeryToken]   // 所有 POST 都要驗防偽 token（GET 不驗）。token 放在 RequestVerificationToken 標頭，見規格 0-6
    public sealed class CReviewApiController(
        dbVenueContext db,
        ICurrentUserService currentUserService,
        IVisitReviewTicketFactory factory,
        ITimeService timeService,
        IReviewScreeningService screening) : ControllerBase
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUserService _currentUser = currentUserService;
        private readonly IVisitReviewTicketFactory _reviewTicketFactory = factory;
        private readonly ITimeService _timeService = timeService;
        private readonly IReviewScreeningService _screening = screening;

        private const string KindVisit = ReviewKind.Visit;
        private const string KindBooking = ReviewKind.Booking;

        /// <summary>
        /// 評論內容的字數上限，直接讀 ReviewCreateInputVM 上的 [StringLength(1000)]。
        ///
        /// 為什麼不寫死 1000：這個數字已經寫在 VM 的驗證屬性上了，
        /// 這裡再寫一次就是兩個地方各記一份，改了一邊忘了另一邊，
        /// 前端顯示的上限就會跟後端擋的上限對不起來。
        /// static readonly 只在第一次用到時讀一次，之後不再讀。
        /// </summary>
        private static readonly int ContentMaxLength =
            typeof(ReviewCreateInputVM)
                .GetProperty(nameof(ReviewCreateInputVM.ReviewContent))!
                .GetCustomAttribute<StringLengthAttribute>()!
                .MaximumLength;

        // ════════════════════════════════════════════════════════
        //  第一區：統一的失敗回應（規格 0-3）
        // ════════════════════════════════════════════════════════

        private const string MsgNotFound = "查無指定評論";
        private const string MsgExpired = "超過可以評論的時間囉，下次請早";
        private const string MsgAlreadyReviewed = "這張憑證已經評論過了";

        private ObjectResult Fail(int status, string errorCode, string message, object? data = null)
            => StatusCode(status, new ApiResult<object?>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Data = data
            });

        private ObjectResult NotFoundResult() => Fail(404, "NotFound", MsgNotFound);

        // 9/29：原本的 ValidationFailed()（把 ModelState 整理成規格 0-5）搬到 Helpers/ApiResponses.InvalidModelState，
        //       由 [ApiController] 自動呼叫，全站 API 共用。

        // ════════════════════════════════════════════════════════
        //  第二區：資格判定（照搬 CReviewController，規則沒有改）
        // ════════════════════════════════════════════════════════

        private enum EligState { Ok, NotFound, Expired, AlreadyReviewed }

        private sealed record VisitTicket(EligState State, ReviewPerVisit? Ticket, ReviewMain? ExistingReview);

        private sealed record BookingTicket(EligState State, ReviewPerBooking? Ticket, ReviewMain? ExistingReview);

        /// <summary>現場評論：用 QRToken 判定資格。含「報到系統漏呼叫工廠」的補償。</summary>
        private async Task<VisitTicket> ResolveVisitTicketAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new(EligState.NotFound, null, null);

            string qrToken = token.Trim();
            var ticket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.Qrtoken == qrToken);
            if (ticket == null)
            {
                // 補償：這張票如果確實已經報到過，就在這裡當場補建憑證
                if (!await _reviewTicketFactory.CreateReviewPerVisitAsync(qrToken))
                    return new(EligState.NotFound, null, null);

                ticket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.Qrtoken == qrToken);
                if (ticket == null)
                    return new(EligState.NotFound, null, null);
            }

            var existing = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);

            if (_timeService.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null);

            return new(EligState.Ok, ticket, null);
        }

        /// <summary>
        /// 預約評論：用 ReviewPerBookingId 判定資格，並比對擁有者。
        /// ⚠️ 不是本人時回 NotFound，不讓人分辨出「這張憑證存在但不是你的」。
        /// </summary>
        private async Task<BookingTicket> ResolveBookingTicketAsync(int id)
        {
            if (id <= 0)
                return new(EligState.NotFound, null, null);

            var ticket = await _db.ReviewPerBookings.FirstOrDefaultAsync(b => b.ReviewPerBookingId == id);
            if (ticket == null)
                return new(EligState.NotFound, null, null);

            if (_currentUser.UserId != ticket.UserId)
                return new(EligState.NotFound, null, null);

            var existing = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewPerBookingId == ticket.ReviewPerBookingId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);

            if (_timeService.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null);

            return new(EligState.Ok, ticket, null);
        }

        /// <summary>
        /// 撰寫用：不是 Ok 就回對應的錯誤（規格 0-3）。回傳 null 代表「可以繼續」。
        /// Razor 版要分兩個多載，是因為「已評過」要轉去的網址參數不同；
        /// API 版只回錯誤碼、由前端決定去哪，所以一個就夠了。
        /// </summary>
        private ObjectResult? RejectIfCannotWrite(EligState state) => state switch
        {
            EligState.Ok              => null,
            EligState.NotFound        => NotFoundResult(),
            EligState.Expired         => Fail(410, "Expired", MsgExpired),
            EligState.AlreadyReviewed => Fail(409, "AlreadyReviewed", MsgAlreadyReviewed),
            // 四種狀態都列在上面了。會走到這裡代表有人新增了狀態卻忘了處理，
            // 直接丟例外讓它在開發時就被看到，不要默默當成「可以評論」放行。
            _ => throw new InvalidOperationException($"未處理的資格狀態：{state}")
        };

        // ════════════════════════════════════════════════════════
        //  第三區：組回應
        // ════════════════════════════════════════════════════════

        private static ReviewContextDto VisitContext(string? venueName, DateTime rentStartTime)
            => ReviewMapper.VisitContext(venueName, rentStartTime);

        private async Task<string> ResolveDisplayNameAsync(ReviewMain review)
        {
            if (review.IsAnonymous)
                return review.AnonymousNickname ?? "匿名使用者";

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == review.UserId);
            return user?.Name ?? "已停用的帳號";
        }

        /// <summary>顯示名稱要查資料庫，在這裡查好；其餘的轉換在 ReviewMapper.ToMyReview。</summary>
        private async Task<MyReviewDto> BuildMyReviewAsync(ReviewMain review, string kind, ReviewContextDto context)
            => ReviewMapper.ToMyReview(review, kind, context, await ResolveDisplayNameAsync(review));

        // ── 評論專區用（照搬 CReviewController）──────────────

        /// <summary>
        /// 上架條件。唯一一份（Razor 版刪掉之後）。
        /// 只有現場評論、非垃圾、顧客選了公開，而且「已回覆」或「建立超過緩衝天數」。
        /// 最後那條是刻意的：館方不作為的預設結果是公開，不能用不回覆壓著負評。
        /// </summary>
        private IQueryable<ReviewMain> PublishedReviews(DateTime now)
        {
            DateTime cutoff = now.AddDays(-ReviewPolicy.PublicBufferDays);

            return _db.ReviewMains.Where(r =>
                r.ReviewPerVisitId != null
                && r.SpamMarkedAt == null
                && r.IsPublic
                && (r.RepliedAt != null || r.CreatedAt <= cutoff));
        }

        /// <summary>類型、時間範圍、有無內容。星等「不」放進來——分布圖要用這個結果算。</summary>
        private IQueryable<ReviewMain> ApplyCardFilters(
            IQueryable<ReviewMain> q, int? sportTypeId, string range, bool hasContentOnly, DateTime todayStart)
        {
            if (sportTypeId is int st)
            {
                var visitIds = _db.ReviewPerVisits
                                  .Where(v => v.SportTypeId == st)
                                  .Select(v => v.ReviewPerVisitId);
                q = q.Where(r => visitIds.Contains(r.ReviewPerVisitId!.Value));
            }

            int? days = ReviewRange.DaysOf(range);
            if (days != null)
            {
                DateTime from = todayStart.AddDays(-days.Value);
                q = q.Where(r => r.CreatedAt >= from);
            }

            if (hasContentOnly)
                q = q.Where(r => r.ReviewContent != null && r.ReviewContent != "");

            return q;
        }

        private static IQueryable<ReviewMain> ApplyCardSort(IQueryable<ReviewMain> q, string sort) => sort switch
        {
            ReviewSort.Newest  => q.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.StarRating),
            ReviewSort.Highest => q.OrderByDescending(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            ReviewSort.Lowest  => q.OrderBy(r => r.StarRating).ThenByDescending(r => r.CreatedAt),
            _                  => q.OrderByDescending(r => r.CreatedAt)
        };

        private static string NormalizeRange(string? range) => range switch
        {
            ReviewRange.Week  => ReviewRange.Week,
            ReviewRange.Month => ReviewRange.Month,
            ReviewRange.Year  => ReviewRange.Year,     // ⚠️ Razor 版漏了這一行，見檔尾「跟 Razor 版不同的地方」
            ReviewRange.All   => ReviewRange.All,
            _                 => ReviewRange.Default
        };

        private static string NormalizeSort(string? sort) => sort switch
        {
            ReviewSort.Newest  => ReviewSort.Newest,
            ReviewSort.Highest => ReviewSort.Highest,
            ReviewSort.Lowest  => ReviewSort.Lowest,
            _                  => ReviewSort.Default
        };

        private static int? NormalizeStar(int? star) => star is >= 1 and <= 5 ? star : null;

        /// <summary>
        /// 先組成既有的 ReviewCardVM（顯示名稱、IsRatingOnly 這些規則都在 VM 裡），
        /// 再用 ReviewMapper.ToPublicCard 轉成 API 的形狀（轉的時候丟掉 ReviewId）。
        /// 跨表查詢照舊：收集 id → 每張表各查一次 → Dictionary 取值。
        /// </summary>
        private async Task<List<PublicReviewCardDto>> BuildCardsAsync(List<ReviewMain> reviews)
        {
            if (reviews.Count == 0) return new List<PublicReviewCardDto>();

            var visitIds = reviews.Where(r => r.ReviewPerVisitId != null)
                                  .Select(r => r.ReviewPerVisitId!.Value)
                                  .Distinct().ToList();
            var visits = await _db.ReviewPerVisits
                                  .Where(v => visitIds.Contains(v.ReviewPerVisitId))
                                  .ToDictionaryAsync(v => v.ReviewPerVisitId);

            var venueIds = visits.Values.Select(v => v.VenueId).Distinct().ToList();
            var venueNames = await _db.Venues
                                      .Where(v => venueIds.Contains(v.VenueId))
                                      .ToDictionaryAsync(v => v.VenueId, v => v.VenueName);

            var userIds = reviews.Where(r => !r.IsAnonymous && r.UserId != null)
                                 .Select(r => r.UserId!.Value)
                                 .Distinct().ToList();
            var userNames = await _db.Users
                                     .Where(u => userIds.Contains(u.UserId))
                                     .ToDictionaryAsync(u => u.UserId, u => u.Name);

            return reviews.Select(r =>
            {
                ReviewPerVisit? visit = r.ReviewPerVisitId is int vid ? visits.GetValueOrDefault(vid) : null;

                var card = new ReviewCardVM
                {
                    ReviewId      = r.ReviewId,
                    StarRating    = r.StarRating,
                    Content       = r.ReviewContent,
                    CreatedAt     = r.CreatedAt,
                    MentionsVenue = r.MentionsVenue,
                    MentionsStaff = r.MentionsStaff,
                    VenueName     = visit != null ? venueNames.GetValueOrDefault(visit.VenueId) : null,
                    DisplayName   = ReviewCardVM.ResolveDisplayName(
                                        r.IsAnonymous, r.AnonymousNickname,
                                        r.UserId is int uid ? userNames.GetValueOrDefault(uid) : null),
                    ReplyContent  = r.ReplyContent,
                    RepliedAt     = r.RepliedAt
                };

                return ReviewMapper.ToPublicCard(card);
            }).ToList();
        }

        // ── 寫入用（照搬）─────────────────────────────────────

        /// <summary>由輸入組出要存的評論。visitId 與 bookingId 一定只有一個有值（XOR 約束）。</summary>
        private ReviewMain BuildNewReview(ReviewCreateInputVM vm, int? visitId, int? bookingId, int? userId)
        {
            string? nickname = vm.IsAnonymous ? NicknameGenerator.Generate() : null;

            return new ReviewMain
            {
                ReviewPerVisitId = visitId,
                ReviewPerBookingId = bookingId,
                UserId = userId,
                StarRating = vm.StarRating!.Value,               // ModelState 已保證不為 null
                ReviewContent = string.IsNullOrWhiteSpace(vm.ReviewContent)
                                  ? null                         // 純空白存 null，否則撞 Content_NotBlank
                                  : vm.ReviewContent.Trim(),
                IsAnonymous = vm.IsAnonymous,
                IsPublic = vm.IsPublic,
                MentionsVenue = vm.MentionsVenue,
                MentionsStaff = vm.MentionsStaff,
                AnonymousNickname = nickname,
                CreatedAt = _timeService.Now
            };
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

        private async Task<IActionResult> SetSatisfactionCoreAsync(ReviewMain review, SatisfactionRequest? body)
        {
            if (review.RepliedAt == null)
                return Fail(409, "NoReply", "館方還沒有回覆，無法表態");
            if (review.ReplySatisfaction != null)
                return Fail(409, "AlreadyRated", "你已經表態過了");

            review.ReplyViewedAt ??= _timeService.Now;   // 保險：約束要求表態前一定已讀
            review.ReplySatisfaction = body!.Satisfaction!.Value;
            await _db.SaveChangesAsync();

            return Ok(ApiResult<SatisfactionResult>.Ok(new SatisfactionResult(review.ReplySatisfaction.Value)));
        }

        private static bool IsValidSatisfaction(SatisfactionRequest? body)
            => body?.Satisfaction is byte s && s <= 2;

        private ObjectResult InvalidSatisfaction() => Fail(400, "InvalidSatisfaction", "輸入異常，請重試");

        // ════════════════════════════════════════════════════════
        //  第四區：Action
        //  現場評論與預約評論分成兩個 Action，而不是共用一個 {kind}：
        //    1. 預約評論要掛 [Authorize(Roles = Member)]，現場評論不能掛。
        //       attribute 是掛在 Action 上的，共用一個 Action 就沒辦法分開掛。
        //    2. 預約評論的 id 是整數，路由寫 {id:int}，不是整數的網址直接 404，
        //       不必進 Action 再檢查。
        //  兩邊共用的邏輯都在上面的私有方法裡，Action 本身只剩「接參數 → 呼叫 → 回應」。
        // ════════════════════════════════════════════════════════

        // ── 2-1 評論專區 ─────────────────────────────────────

        [HttpGet("")]
        public async Task<IActionResult> GetPublicReviews(
            int? sportTypeId, string? range, int? star, bool hasContentOnly = false, string? sort = null)
        {
            string rg = NormalizeRange(range);
            string so = NormalizeSort(sort);
            int? st = NormalizeStar(star);

            DateTime now = _timeService.Now;
            DateTime todayStart = _timeService.Today;

            var sportTabs = new List<SportTabVM> { new(null, "全部") };
            sportTabs.AddRange(await _db.SportTypes
                                        .Where(s => s.IsActive)
                                        .OrderBy(s => s.SportTypeId)
                                        .Select(s => new SportTabVM(s.SportTypeId, s.SportName))
                                        .ToListAsync());

            // 星等篩選之前的結果：分布圖與平均分數用這一份算
            var beforeStar = ApplyCardFilters(PublishedReviews(now), sportTypeId, rg, hasContentOnly, todayStart);

            var counts = await beforeStar
                .GroupBy(r => r.StarRating)
                .Select(g => new { Star = g.Key, Count = g.Count() })
                .ToListAsync();

            int CountOf(int n) => counts.FirstOrDefault(c => c.Star == n)?.Count ?? 0;

            // 沿用 StarDistributionVM：平均、百分比的算法（含「總數為 0」的處理）只寫在那裡
            var dist = new StarDistributionVM
            {
                Star5 = CountOf(5), Star4 = CountOf(4), Star3 = CountOf(3), Star2 = CountOf(2), Star1 = CountOf(1)
            };
            var bars = new[] { 5, 4, 3, 2, 1 }
                .Select(s => new StarBarDto(s, dist.CountOf(s), dist.PercentOf(s)))
                .ToList();

            var listQuery = beforeStar;
            if (st is int s)
                listQuery = listQuery.Where(r => r.StarRating == s);

            // ⚠️ 還沒有分頁。資料量變大之後要補 Skip / Take（規格已經預留 page／totalPages）。
            var reviews = await ApplyCardSort(listQuery, so).ToListAsync();

            var data = new PublicReviewListDto(
                Filter: new ReviewFilterDto(sportTypeId, rg, st, hasContentOnly, so),
                Defaults: new ReviewDefaultsDto(ReviewRange.Default, ReviewSort.Default),
                Options: new ReviewOptionsDto(
                    ReviewRange.Options.Select(o => new OptionItem(o.Value, o.Text)).ToList(),
                    ReviewSort.Options.Select(o => new OptionItem(o.Value, o.Text)).ToList()),
                SportTabs: sportTabs,
                Summary: new RatingSummaryDto(dist.Average, dist.Total, bars),
                Items: await BuildCardsAsync(reviews),
                Page: 1,
                TotalPages: 1);

            return Ok(ApiResult<PublicReviewListDto>.Ok(data));
        }

        // ── 2-2 撰寫頁設定 ───────────────────────────────────

        [HttpGet("visit/{token}/form")]
        public async Task<IActionResult> GetVisitForm(string token)
        {
            var r = await ResolveVisitTicketAsync(token);
            var reject = RejectIfCannotWrite(r.State);
            if (reject != null) return reject;

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.VenueId == r.Ticket!.VenueId);
            bool loggedIn = _currentUser.UserId != null;

            var data = new WriteFormDto(
                Kind: KindVisit,
                Context: VisitContext(venue?.VenueName, r.Ticket!.RentStartTime),
                Form: new WriteFormOptionsDto(
                    ShowMentions: true,
                    ShowAnonymous: true,
                    CanChooseAnonymous: loggedIn,
                    ShowPublic: true,
                    Initial: new WriteFormInitialDto(IsAnonymous: !loggedIn, IsPublic: true),   // 未登入 → 鎖定匿名
                    ContentMaxLength: ContentMaxLength));

            return Ok(ApiResult<WriteFormDto>.Ok(data));
        }

        [HttpGet("booking/{id:int}/form")]
        [Authorize(Roles = RoleNames.Member)]
        public async Task<IActionResult> GetBookingForm(int id)
        {
            var r = await ResolveBookingTicketAsync(id);
            var reject = RejectIfCannotWrite(r.State);
            if (reject != null) return reject;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == r.Ticket!.OrderId);

            // 預約評論不公開、不匿名；提及標籤照目前的 CreateForBooking.cshtml，先不顯示
            var data = new WriteFormDto(
                Kind: KindBooking,
                Context: ReviewMapper.BookingContext(r.Ticket!.OrderId, order?.OrderNo, r.Ticket.PaymentMethod),
                Form: new WriteFormOptionsDto(
                    ShowMentions: false,
                    ShowAnonymous: false,
                    CanChooseAnonymous: false,
                    ShowPublic: false,
                    Initial: new WriteFormInitialDto(IsAnonymous: false, IsPublic: false),
                    ContentMaxLength: ContentMaxLength));

            return Ok(ApiResult<WriteFormDto>.Ok(data));
        }

        // ── 2-3 送出評論 ─────────────────────────────────────

        [HttpPost("visit/{token}")]
        public async Task<IActionResult> CreateVisit(string token, [FromBody] ReviewCreateForVisitVM? vm)
        {
            // ⚠️ 資格要重驗一次：表單可能停在畫面上好幾天，也可能有人繞過畫面直接送請求
            var r = await ResolveVisitTicketAsync(token);
            var reject = RejectIfCannotWrite(r.State);
            if (reject != null) return reject;

            // Razor 版在這裡還要比對「表單帶回的 ReviewPerVisitId」跟憑證是否同一張。
            // API 版的本體裡沒有 id，識別碼只有網址上的 token 一個，所以這個檢查不存在了。

            // 欄位驗證（星等必填、字數上限⋯）已經由 [ApiController] 在進來之前做完了；這裡只剩「整個本體沒送」
            if (vm == null) return BadRequest(ApiResponses.EmptyBody());
            int? userId = _currentUser.UserId;
            if (userId == null)
                vm.IsAnonymous = true;   // 前端鎖住擋不住直接送請求的人

            var review = BuildNewReview(vm, r.Ticket!.ReviewPerVisitId, null, userId);
            _db.ReviewMains.Add(review);
            await _db.SaveChangesAsync();
            await _screening.CreateForReviewAsync(review);   // 評論預審：建立預審紀錄（沒設定金鑰時什麼都不做）

            return Ok(ApiResultVM.Ok("評論已送出"));
        }

        [HttpPost("booking/{id:int}")]
        [Authorize(Roles = RoleNames.Member)]
        public async Task<IActionResult> CreateBooking(int id, [FromBody] ReviewCreateForBookingVM? vm)
        {
            var r = await ResolveBookingTicketAsync(id);
            var reject = RejectIfCannotWrite(r.State);
            if (reject != null) return reject;

            // 欄位驗證（星等必填、字數上限⋯）已經由 [ApiController] 在進來之前做完了；這裡只剩「整個本體沒送」
            if (vm == null) return BadRequest(ApiResponses.EmptyBody());
            // 預約評論一律不公開、不匿名，不接受前端送來的值
            vm.IsPublic = false;
            vm.IsAnonymous = false;

            var review = BuildNewReview(vm, null, r.Ticket!.ReviewPerBookingId, _currentUser.UserId);
            _db.ReviewMains.Add(review);
            await _db.SaveChangesAsync();
            await _screening.CreateForReviewAsync(review);   // 評論預審：建立預審紀錄（沒設定金鑰時什麼都不做）

            return Ok(ApiResultVM.Ok("評論已送出"));
        }

        // ── 2-4 我的評論（純讀取，不寫入已讀）─────────────────

        [HttpGet("visit/{token}")]
        public async Task<IActionResult> GetVisitReview(string token)
        {
            var r = await ResolveVisitTicketAsync(token);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.VenueId == r.Ticket!.VenueId);
            var data = await BuildMyReviewAsync(r.ExistingReview!, KindVisit,
                                                VisitContext(venue?.VenueName, r.Ticket!.RentStartTime));

            return Ok(ApiResult<MyReviewDto>.Ok(data));
        }

        [HttpGet("booking/{id:int}")]
        public async Task<IActionResult> GetBookingReview(int id)
        {
            var r = await ResolveBookingTicketAsync(id);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == r.Ticket!.OrderId);
            var data = await BuildMyReviewAsync(r.ExistingReview!, KindBooking,
                                                new ReviewContextDto(order?.OrderNo, null));

            return Ok(ApiResult<MyReviewDto>.Ok(data));
        }

        // ── 2-8 員工預覽 ─────────────────────────────────────

        /// <summary>
        /// 員工從館方清單打開「以顧客視角預覽」。回傳的形狀跟 2-4 一模一樣，
        /// 前端用同一個畫面顯示，所以員工看到的就是顧客看到的。
        ///
        /// 為什麼另開一支，不讓員工直接打 2-4：
        ///   1. 2-4 要憑證（QR token／本人登入）。員工不該拿到顧客的 QR token，
        ///      預約評論也要會員本人登入才看得到，員工打不開。
        ///   2. 這支只有後台角色能用，所以網址可以直接用 ReviewId——
        ///      「不用 ReviewId 當網址參數」防的是「沒有權限的人從 1 掃到 100」，這裡進得來的本來就看得到全部。
        ///
        /// ⚠️ 純讀取：不寫入 ReplyViewedAt（員工看過不等於顧客看過）。
        /// </summary>
        [HttpGet("preview/{reviewId:int}")]
        [Authorize(Roles = RoleNames.BackOffice)]
        public async Task<IActionResult> GetPreview(int reviewId)
        {
            var review = await _db.ReviewMains.FirstOrDefaultAsync(r => r.ReviewId == reviewId);
            if (review == null) return NotFoundResult();

            MyReviewDto data;

            if (review.ReviewPerVisitId is int visitId)
            {
                var ticket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.ReviewPerVisitId == visitId);
                if (ticket == null) return NotFoundResult();

                var venue = await _db.Venues.FirstOrDefaultAsync(v => v.VenueId == ticket.VenueId);
                data = await BuildMyReviewAsync(review, KindVisit, VisitContext(venue?.VenueName, ticket.RentStartTime));
            }
            else if (review.ReviewPerBookingId is int bookingId)
            {
                var ticket = await _db.ReviewPerBookings.FirstOrDefaultAsync(b => b.ReviewPerBookingId == bookingId);
                if (ticket == null) return NotFoundResult();

                var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == ticket.OrderId);
                data = await BuildMyReviewAsync(review, KindBooking, new ReviewContextDto(order?.OrderNo, null));
            }
            else
            {
                // 資料庫的 XOR 約束保證兩個一定剛好有一個，理論上到不了這裡。
                // 真的到了代表資料異常，當成查不到，不要回一個種類不明的畫面。
                return NotFoundResult();
            }

            return Ok(ApiResult<MyReviewDto>.Ok(data));
        }

        // ── 2-5 記錄已讀 ─────────────────────────────────────

        [HttpPost("visit/{token}/reply-viewed")]
        public async Task<IActionResult> MarkVisitReplyViewed(string token)
        {
            var r = await ResolveVisitTicketAsync(token);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            bool viewed = await MarkReplyViewedIfNeededAsync(r.ExistingReview!);
            return Ok(ApiResult<ReplyViewedResult>.Ok(new ReplyViewedResult(viewed)));
        }

        [HttpPost("booking/{id:int}/reply-viewed")]
        public async Task<IActionResult> MarkBookingReplyViewed(int id)
        {
            var r = await ResolveBookingTicketAsync(id);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            bool viewed = await MarkReplyViewedIfNeededAsync(r.ExistingReview!);
            return Ok(ApiResult<ReplyViewedResult>.Ok(new ReplyViewedResult(viewed)));
        }

        // ── 2-6 切換公開（只有現場評論）───────────────────────

        [HttpPost("visit/{token}/visibility")]
        public async Task<IActionResult> SetVisibility(string token, [FromBody] VisibilityRequest? body)
        {
            if (body?.IsPublic is not bool isPublic)
                return Fail(400, "ValidationFailed", "請指定要公開或不公開",
                            new { errors = new Dictionary<string, string[]> { ["isPublic"] = new[] { "請指定要公開或不公開" } } });

            var r = await ResolveVisitTicketAsync(token);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            var review = r.ExistingReview!;

            // 被標記垃圾的評論強制不公開。Razor 版是安靜地轉回頁面；API 版明確回錯誤碼
            if (review.SpamMarkedAt != null)
                return Fail(409, "SpamMarked", "這則評論已下架，無法切換公開狀態");

            review.IsPublic = isPublic;
            await _db.SaveChangesAsync();

            return Ok(ApiResult<VisibilityResult>.Ok(new VisibilityResult(review.IsPublic)));
        }

        // ── 2-7 對回覆表態 ───────────────────────────────────

        [HttpPost("visit/{token}/satisfaction")]
        public async Task<IActionResult> SetVisitSatisfaction(string token, [FromBody] SatisfactionRequest? body)
        {
            if (!IsValidSatisfaction(body)) return InvalidSatisfaction();

            var r = await ResolveVisitTicketAsync(token);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            return await SetSatisfactionCoreAsync(r.ExistingReview!, body);
        }

        [HttpPost("booking/{id:int}/satisfaction")]
        public async Task<IActionResult> SetBookingSatisfaction(int id, [FromBody] SatisfactionRequest? body)
        {
            if (!IsValidSatisfaction(body)) return InvalidSatisfaction();

            var r = await ResolveBookingTicketAsync(id);
            if (r.State != EligState.AlreadyReviewed) return NotFoundResult();

            return await SetSatisfactionCoreAsync(r.ExistingReview!, body);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  跟 Razor 版不同的地方（規則面）
    //
    //  1. NormalizeRange 補上 ReviewRange.Year。
    //     Razor 版的 switch 只列了 Week／Month／All，「一年內」要靠「不認得 → 回預設」才選得到。
    //     剛好預設就是 Year，所以畫面上看不出來；但只要哪天預設改成別的，
    //     「一年內」這個選項就會選了等於沒選。這裡補上，讓每個選項都被明確認得。
    //     （Razor 版要不要一起補，由你決定；它之後會被刪掉。）
    //
    //  2. 「我的評論」的 GET 不再寫入 ReplyViewedAt，改成另外一支 POST（規格 2-5）。
    //
    //  3. 失敗不再「轉址 + TempData」，改回狀態碼 + ApiResultVM（規格 0-3）。
    //
    //  4. 欄位驗證失敗先於資格判定（因為 [ApiController]），見檔頭說明。
    // ════════════════════════════════════════════════════════════════
}
