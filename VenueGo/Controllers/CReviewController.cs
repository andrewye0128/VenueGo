using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Controllers
{
    public class CReviewController(dbVenueContext db, ICurrentUserService currentUserService, IVisitReviewTicketFactory factory, ITimeService timeService) : Controller
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUserService _currentUser = currentUserService;
        // 只注入「現場評論」那個介面：這支 Controller 的補償邏輯只會用到
        // CreateReviewPerVisitAsync，不該看得到預約評論的方法。
        private readonly IVisitReviewTicketFactory _reviewTicketFactory = factory;
        private readonly ITimeService _timeService = timeService;

        // ════════════════════════════════════════════════════════
        //  關於 async：為什麼整支改成非同步
        //
        //  async 不會讓「一個請求」變快——查資料庫要 50 毫秒就是 50 毫秒。
        //  它做的是：等資料庫回應的那段時間，把執行緒還給執行緒池去服務
        //  別的請求，而不是呆站在那裡等。換到的是「同時很多人用時能撐住
        //  多少人」，不是「一個人用時有多快」。
        //
        //  ⚠️ Action 方法的名字「不要」加 Async 結尾。
        //     路由是用方法名字對應網址的，而 RedirectToAction(nameof(X))
        //     會把方法名原樣當成 action 名。加了 Async 之後兩邊會對不上，
        //     而且是執行時才壞、編譯不會報錯。私有方法沒有這個問題，
        //     所以下面的私有方法照慣例加了 Async。
        // ════════════════════════════════════════════════════════

        // ════════════════════════════════════════════════════════
        //  第一區：資格判定
        //  這一區只回答「能不能評論」，不碰畫面、不組 VM、不寫資料。
        // ════════════════════════════════════════════════════════

        // 用 enum 而不是 bool，因為「不能評論」有三種原因，呼叫端要分別處理。
        private enum EligState { Ok, NotFound, Expired, AlreadyReviewed }

        // record 是「一次建好就不能改的小資料袋」，一行等於一個有三個唯讀屬性的類別。
        private sealed record VisitTicket(
            EligState State,
            ReviewPerVisit? Ticket,
            ReviewMain? ExistingReview);

        private sealed record BookingTicket(
            EligState State,
            ReviewPerBooking? Ticket,
            ReviewMain? ExistingReview);

        /// <summary>現場評論：用 QRToken 判定資格。</summary>
        private async Task<VisitTicket> ResolveVisitTicketAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new(EligState.NotFound, null, null);          // 未輸入 ❌

            string qrToken = token.Trim();
            var ticket = await _db.ReviewPerVisits
                                  .FirstOrDefaultAsync(v => v.Qrtoken == qrToken);
            if (ticket == null)
            {
                // 補償：報到系統可能漏了呼叫工廠。這張票如果確實已經報到過，
                // 就在這裡當場補建憑證，顧客不會因為上游漏呼叫而評不了論。
                if (await _reviewTicketFactory.CreateReviewPerVisitAsync(qrToken))
                {
                    ticket = await _db.ReviewPerVisits.FirstOrDefaultAsync(v => v.Qrtoken == qrToken);
                    if (ticket == null) 
                        return new(EligState.NotFound, null, null);      // 憑證補建失敗 ❌
                }
                else
                {
                    return new(EligState.NotFound, null, null);          // 查無憑證 ❌
                }
            }

            var existing = await _db.ReviewMains
                                    .FirstOrDefaultAsync(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);  // 已評過 📋

            if (_timeService.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null);         // 已逾期 ❌

            return new(EligState.Ok, ticket, null);                  // 可以評論 👌
        }

        /// <summary>
        /// 預約評論：用 ReviewPerBookingId 判定資格。
        /// 現場評論靠 QRToken 守門（64 字元猜不到），預約評論的 id 是連號整數，
        /// 所以必須額外比對擁有者。
        /// ⚠️ 不是本人時回傳 NotFound 而不是另開一個「無權限」狀態——
        ///    不要讓人從錯誤訊息分辨出「這張憑證存在但不是你的」。
        /// </summary>
        private async Task<BookingTicket> ResolveBookingTicketAsync(int? id)
        {
            if (id == null || id <= 0)
                return new(EligState.NotFound, null, null);          // 無效輸入 ❌

            var ticket = await _db.ReviewPerBookings
                                  .FirstOrDefaultAsync(b => b.ReviewPerBookingId == id);
            if (ticket == null)
                return new(EligState.NotFound, null, null);          // 查無憑證 ❌

            // TODO: 登入方提供驗證方法後換掉這一行
            if (_currentUser.UserId != ticket.UserId)
                return new(EligState.NotFound, null, null);          // 不是你的 ❌

            var existing = await _db.ReviewMains
                                    .FirstOrDefaultAsync(r => r.ReviewPerBookingId == ticket.ReviewPerBookingId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);  // 已評過 📋

            if (_timeService.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null);         // 已逾期 ❌

            return new(EligState.Ok, ticket, null);                  // 可以評論 👌
        }

        // ── 把「非 Ok 狀態該怎麼回應」也集中起來 ─────────────
        //
        //  回傳 null 代表「狀態是 Ok，請繼續往下做」。
        //  呼叫端固定寫成兩行：
        //      var reject = RejectIfNotOk(r);
        //      if (reject != null) return reject;
        //
        //  兩種憑證各寫一個，是因為「已評過」要轉去的網址參數不同，
        //  而且 Ticket 的型別不一樣。內容雖然像，但硬合成一個要用泛型
        //  或委派，讀起來比現在難懂，不划算。
        //
        //  這兩個不碰資料庫，所以維持同步——不是所有方法都要跟著變 async，
        //  只有真的在等 I/O 的才需要。

        private IActionResult? RejectIfNotOk(VisitTicket r)
        {
            switch (r.State)
            {
                case EligState.NotFound:
                    TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                    return RedirectToAction(nameof(Index));

                case EligState.Expired:
                    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                    return RedirectToAction(nameof(Index));

                case EligState.AlreadyReviewed:
                    return RedirectToAction(nameof(ShowMyReviewPage),
                                            new { token = r.Ticket!.Qrtoken });

                default:
                    return null;    // Ok
            }
        }

        private IActionResult? RejectIfNotOk(BookingTicket r)
        {
            switch (r.State)
            {
                case EligState.NotFound:
                    TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                    return RedirectToAction(nameof(Index));

                case EligState.Expired:
                    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                    return RedirectToAction(nameof(Index));

                case EligState.AlreadyReviewed:
                    return RedirectToAction(nameof(ShowMyReviewPage),
                                            new { bookingId = r.Ticket!.ReviewPerBookingId });

                default:
                    return null;    // Ok
            }
        }

        // ════════════════════════════════════════════════════════
        //  第二區：組 ViewModel
        //  這一區只負責把實體翻譯成畫面要的東西，不做判斷、不寫資料。
        // ════════════════════════════════════════════════════════

        private async Task<ReviewCreateForVisitVM> BuildCreateVmAsync(ReviewPerVisit perVisit)
        {
            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.VenueId == perVisit.VenueId);

            return new ReviewCreateForVisitVM
            {
                ReviewPerVisitId = perVisit.ReviewPerVisitId,
                QrToken = perVisit.Qrtoken,
                VenueName = venue?.VenueName,
                RentStartTime = perVisit.RentStartTime,

                StarRating = null,
                ReviewContent = null,
                MentionsVenue = false,
                MentionsStaff = false,
                CanChooseAnonymous = _currentUser.UserId != null,
                IsAnonymous = _currentUser.UserId == null,  // 未登入 → 鎖定匿名
                IsPublic = true
            };
        }

        private async Task<ReviewCreateForBookingVM> BuildCreateVmAsync(ReviewPerBooking perBooking)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == perBooking.OrderId);

            return new ReviewCreateForBookingVM
            {
                ReviewPerBookingId = perBooking.ReviewPerBookingId,
                OrderId = perBooking.OrderId,
                OrderNo = order?.OrderNo,
                PaymentMethod = perBooking.PaymentMethod,

                StarRating = null,
                ReviewContent = null,
                MentionsVenue = false,
                MentionsStaff = false,

                // 預約評論不公開展示，也沒有匿名的意義——
                // 只有館方看得到，而館方從 OrderId 就查得到是誰。
                CanChooseAnonymous = false,
                IsAnonymous = false,
                IsPublic = false
            };
        }

        /// <summary>
        /// 顯示名稱：匿名用暱稱，實名查會員姓名。
        /// 兩種評論共用。
        /// </summary>
        private async Task<string> ResolveDisplayNameAsync(ReviewMain review)
        {
            if (review.IsAnonymous)
                return review.AnonymousNickname ?? "匿名使用者";

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == review.UserId);
            return user?.Name ?? "已停用的帳號";
        }

        /// <summary>
        /// 檢視頁的 VM。兩種評論共用同一個版面，差別只在
        /// 「用什麼識別自己」與「頁尾顯示什麼」。
        /// </summary>
        private async Task<MyReviewPageVM> BuildMyReviewVmAsync(
            ReviewMain review,
            string? token,          // 現場評論才有
            int? bookingId,         // 預約評論才有
            string? venueName,      // 預約評論為 null
            DateTime? rentStartTime,
            string? orderNo)        // 現場評論為 null
        {
            return new MyReviewPageVM
            {
                ReviewId = review.ReviewId,
                Qrtoken = token,
                ReviewPerBookingId = bookingId,

                StarRating = review.StarRating,
                ReviewContent = review.ReviewContent,
                IsAnonymous = review.IsAnonymous,
                IsPublic = review.IsPublic,
                MentionsVenue = review.MentionsVenue,
                MentionsStaff = review.MentionsStaff,
                CreatedAt = review.CreatedAt,
                DisplayName = await ResolveDisplayNameAsync(review),

                VenueName = venueName,
                RentStartTime = rentStartTime,
                OrderNo = orderNo,

                ReplyContent = review.ReplyContent,
                RepliedAt = review.RepliedAt,
                ReplyViewedAt = review.ReplyViewedAt,
                ReplySatisfaction = review.ReplySatisfaction,
                IsSpamMarked = review.SpamMarkedAt != null
            };
        }

        // ── 評論專區（公開卡片）──────────────────────────

        /// <summary>
        /// 上架條件。這是唯一一份，別的地方要用就呼叫它，不要重打。
        ///
        ///   只有現場評論進顧客端（預約評論的 IsPublic 後端寫死 false）
        ///   非垃圾
        ///   顧客選了公開
        ///   而且「已回覆」或「建立超過緩衝天數」
        ///
        /// 最後那條是刻意的設計：館方不作為的預設結果是公開，
        /// 不能用「不回覆」來把負評壓著不讓它出現。
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

        /// <summary>
        /// 類型、時間範圍、有無內容三個篩選。
        /// 星等篩選「不」放進來——分布圖要用這個結果去算，
        /// 如果連星等也篩掉，點了 5 星之後分布圖就只剩一條了。
        /// </summary>
        private IQueryable<ReviewMain> ApplyCardFilters(
            IQueryable<ReviewMain> q, int? sportTypeId, string range, bool hasContentOnly, DateTime todayStart)
        {
            if (sportTypeId is int st)
            {
                // 沒有導覽屬性，用子查詢接到憑證的運動類型。
                // 保持 IQueryable，EF 會翻成 IN (SELECT …)，不會把資料抓回記憶體。
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

        /// <summary>不認得的星等一律當成「不篩選」。</summary>
        private static int? NormalizeStar(int? star)
            => star is >= 1 and <= 5 ? star : null;

        /// <summary>
        /// 把評論翻譯成卡片。
        /// 沒有導覽屬性，所以照館方清單那套做法：先把評論查出來，
        /// 收集 id，每張表查一次放進 Dictionary，組 VM 時用 id 取。
        /// 不管幾筆，每張表都只查一次。
        /// </summary>
        private async Task<List<ReviewCardVM>> BuildCardsAsync(List<ReviewMain> reviews)
        {
            if (reviews.Count == 0) return new List<ReviewCardVM>();

            // 現場憑證 → 場地名稱
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

            // 實名評論的會員姓名
            var userIds = reviews.Where(r => !r.IsAnonymous && r.UserId != null)
                                 .Select(r => r.UserId!.Value)
                                 .Distinct().ToList();
            var userNames = await _db.Users
                                     .Where(u => userIds.Contains(u.UserId))
                                     .ToDictionaryAsync(u => u.UserId, u => u.Name);

            return reviews.Select(r =>
            {
                ReviewPerVisit? visit = r.ReviewPerVisitId is int vid
                                        ? visits.GetValueOrDefault(vid) : null;

                return new ReviewCardVM
                {
                    ReviewId      = r.ReviewId,
                    StarRating    = r.StarRating,
                    Content       = r.ReviewContent,
                    CreatedAt     = r.CreatedAt,
                    MentionsVenue = r.MentionsVenue,
                    MentionsStaff = r.MentionsStaff,
                    VenueName     = visit != null ? venueNames.GetValueOrDefault(visit.VenueId) : null,

                    // 已經在記憶體裡了，可以放心用這個 static 方法——
                    // 寫在 LINQ 的 select 裡才會害 EF 改成客戶端評估。
                    DisplayName   = ReviewCardVM.ResolveDisplayName(
                                        r.IsAnonymous, r.AnonymousNickname,
                                        r.UserId is int uid ? userNames.GetValueOrDefault(uid) : null),

                    ReplyContent  = r.ReplyContent,
                    RepliedAt     = r.RepliedAt
                };
            }).ToList();
        }

        private async Task<ReviewIndexVM> BuildIndexVmAsync(
            int? sportTypeId, string? range, int? star, bool hasContentOnly, string? sort)
        {
            string rg = NormalizeRange(range);
            string so = NormalizeSort(sort);
            int? st = NormalizeStar(star);

            DateTime now = _timeService.Now;
            DateTime todayStart = _timeService.Today;   // 原本：DateTime.Today;

            // 分頁列：啟用中的運動類型，前面加一個「全部」
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

            var distribution = new StarDistributionVM
            {
                Star5 = CountOf(5),
                Star4 = CountOf(4),
                Star3 = CountOf(3),
                Star2 = CountOf(2),
                Star1 = CountOf(1)
            };

            // 清單才套用星等篩選
            var listQuery = beforeStar;
            if (st is int s)
                listQuery = listQuery.Where(r => r.StarRating == s);

            // ⚠️ 期中沒有分頁。資料量變大之後要補 Skip / Take。
            var reviews = await ApplyCardSort(listQuery, so).ToListAsync();

            return new ReviewIndexVM
            {
                SportTypeId    = sportTypeId,
                TimeRange      = rg,
                Star           = st,
                HasContentOnly = hasContentOnly,
                Sort           = so,

                SportTabs    = sportTabs,
                Distribution = distribution,
                Items        = await BuildCardsAsync(reviews)
            };
        }

        // ════════════════════════════════════════════════════════
        //  第三區：寫入
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 由輸入 VM 組出要存的評論實體。兩種評論共用。
        /// visitId 與 bookingId 一定只有一個有值（XOR 約束）。
        /// 純計算、不碰資料庫，所以維持同步。
        /// </summary>
        private ReviewMain BuildNewReview(
            ReviewCreateInputVM vm, int? visitId, int? bookingId, int? userId)
        {
            // 只有匿名才產生暱稱，實名留 null（= 用會員當下的真實姓名）
            string? nickname = vm.IsAnonymous ? NicknameGenerator.Generate() : null;

            return new ReviewMain
            {
                ReviewPerVisitId = visitId,
                ReviewPerBookingId = bookingId,
                UserId = userId,
                StarRating = vm.StarRating!.Value,      // ModelState 已保證不為 null
                ReviewContent = string.IsNullOrWhiteSpace(vm.ReviewContent)
                                  ? null                // 純空白存 null，
                                  : vm.ReviewContent.Trim(),  // 否則撞 Content_NotBlank
                IsAnonymous = vm.IsAnonymous,
                IsPublic = vm.IsPublic,
                MentionsVenue = vm.MentionsVenue,
                MentionsStaff = vm.MentionsStaff,
                AnonymousNickname = nickname,
                CreatedAt = _timeService.Now
            };
        }

        /// <summary>
        /// 顧客打開檢視頁就代表看到回覆了。
        /// ⚠️ 只在「有回覆且尚未記錄」時寫，否則每次重新整理都會蓋掉原本的時間。
        ///    約束 ReplyViewed_Logic 也要求 RepliedAt 不為 null 且 ReplyViewedAt >= RepliedAt。
        /// </summary>
        private async Task MarkReplyViewedIfNeededAsync(ReviewMain review)
        {
            // 取時間挪到兩個 early return 之後：不需要寫入的情況連問都不必問。
            if (review.RepliedAt == null) return;
            if (review.ReplyViewedAt != null) return;

            review.ReplyViewedAt = _timeService.Now;
            await _db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        //  第四區：Action
        //  到這裡每個 Action 都只剩「接參數 → 呼叫 → 決定回什麼畫面」。
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 評論專區：任何人都看得到的公開評論列表。
        ///
        /// 篩選用一般連結整頁載入，不用 axios——顧客端的網址要能分享、
        /// 能加書籤、能按上一頁。館方清單是內部工具才適合局部更新。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            int? sportTypeId, string? range, int? star, bool hasContentOnly = false, string? sort = null)
        {
            return View(await BuildIndexVmAsync(sportTypeId, range, star, hasContentOnly, sort));
        }

        // ── 現場評論撰寫 ────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> CreateForVisit(string? token)
        {
            var r = await ResolveVisitTicketAsync(token);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            return View(await BuildCreateVmAsync(r.Ticket!));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateForVisit(ReviewCreateForVisitVM vm, string? token)
        {
            // ⚠️ 資格要重驗一次。表單可能停在頁面上好幾天，
            //    也可能有人繞過畫面直接送請求。
            var r = await ResolveVisitTicketAsync(token);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            // 憑證與表單帶回的 id 必須是同一張，防止換 id 評別人的場次
            if (r.Ticket!.ReviewPerVisitId != vm.ReviewPerVisitId)
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "輸入異常，請重試";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                // 驗證失敗回原頁：顯示欄位是從表單繫結來的，可能已經空了，
                // 用憑證重建一次再回去，確認區塊才不會變空白。
                var redo = await BuildCreateVmAsync(r.Ticket);
                redo.StarRating = vm.StarRating;
                redo.ReviewContent = vm.ReviewContent;
                redo.MentionsVenue = vm.MentionsVenue;
                redo.MentionsStaff = vm.MentionsStaff;
                redo.IsAnonymous = vm.IsAnonymous;
                redo.IsPublic = vm.IsPublic;
                return View(redo);
            }

            int? userId = _currentUser.UserId;
            if (userId == null)
                vm.IsAnonymous = true;   // 前端 disabled 擋不住直接送請求的人

            var newReview = BuildNewReview(vm, r.Ticket.ReviewPerVisitId, null, userId);

            _db.ReviewMains.Add(newReview);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { token = r.Ticket.Qrtoken });
        }

        // ── 預約評論撰寫 ────────────────────────────────────

        [HttpGet]
        [Authorize(Roles = RoleNames.Member)]
        public async Task<IActionResult> CreateForBooking(int? id)
        {
            var r = await ResolveBookingTicketAsync(id);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            return View(await BuildCreateVmAsync(r.Ticket!));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNames.Member)]
        public async Task<IActionResult> CreateForBooking(ReviewCreateForBookingVM vm, int? id)
        {
            var r = await ResolveBookingTicketAsync(id);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            if (r.Ticket!.ReviewPerBookingId != vm.ReviewPerBookingId)
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "輸入異常，請重試";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                var redo = await BuildCreateVmAsync(r.Ticket);
                redo.StarRating = vm.StarRating;
                redo.ReviewContent = vm.ReviewContent;
                redo.MentionsVenue = vm.MentionsVenue;
                redo.MentionsStaff = vm.MentionsStaff;
                return View(redo);
            }

            // 預約評論一律不公開、不匿名，不接受表單送來的值
            vm.IsPublic = false;
            vm.IsAnonymous = false;

            var newReview = BuildNewReview(vm, null, r.Ticket.ReviewPerBookingId,
                                           _currentUser.UserId);

            _db.ReviewMains.Add(newReview);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { bookingId = r.Ticket.ReviewPerBookingId });
        }

        // ── 檢視頁 ──────────────────────────────────────────

        /// <summary>
        /// 兩種評論共用一個頁面。token 有值走現場、bookingId 有值走預約。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ShowMyReviewPage(string? token, int? bookingId)
        {
            if (!string.IsNullOrWhiteSpace(token))
                return await ShowVisitReviewAsync(token);

            if (bookingId != null)
                return await ShowBookingReviewAsync(bookingId);

            TempData[CDictionary.TK_MSG_Input錯誤] = "載入時發生異常，請重試";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> ShowVisitReviewAsync(string token)
        {
            var r = await ResolveVisitTicketAsync(token);

            // 這一頁要的是「已經評過」，跟撰寫頁剛好相反，所以不能用 RejectIfNotOk
            if (r.State != EligState.AlreadyReviewed)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction(nameof(Index));
            }

            var review = r.ExistingReview!;
            await MarkReplyViewedIfNeededAsync(review);

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.VenueId == r.Ticket!.VenueId);

            var vm = await BuildMyReviewVmAsync(review,
                                     token: r.Ticket!.Qrtoken,
                                     bookingId: null,
                                     venueName: venue?.VenueName,
                                     rentStartTime: r.Ticket.RentStartTime,
                                     orderNo: null);
            return View(nameof(ShowMyReviewPage), vm);
        }

        private async Task<IActionResult> ShowBookingReviewAsync(int? bookingId)
        {
            var r = await ResolveBookingTicketAsync(bookingId);

            if (r.State != EligState.AlreadyReviewed)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction(nameof(Index));
            }

            var review = r.ExistingReview!;
            await MarkReplyViewedIfNeededAsync(review);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == r.Ticket!.OrderId);

            var vm = await BuildMyReviewVmAsync(review,
                                     token: null,
                                     bookingId: r.Ticket!.ReviewPerBookingId,
                                     venueName: null,
                                     rentStartTime: null,
                                     orderNo: order?.OrderNo);
            return View(nameof(ShowMyReviewPage), vm);
        }

        // ── 檢視頁上的操作 ──────────────────────────────────

        /// <summary>
        /// 切換公開狀態。只有現場評論做得到——預約評論一律不公開。
        /// token 從表單的 hidden 欄位帶上來（模型繫結會從表單本體找）。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVisibility(string? token, bool isPublic)
        {
            var r = await ResolveVisitTicketAsync(token);
            if (r.State != EligState.AlreadyReviewed)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction(nameof(Index));
            }

            var review = r.ExistingReview!;

            // 被標記垃圾的評論強制不公開，不給切回來
            if (review.SpamMarkedAt != null)
                return RedirectToAction(nameof(ShowMyReviewPage),
                                        new { token = r.Ticket!.Qrtoken });

            review.IsPublic = isPublic;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { token = r.Ticket!.Qrtoken });
        }

        /// <summary>
        /// 對館方回覆表態。0 不滿意 / 1 普通 / 2 滿意。
        /// ⚠️ 約束 ReplySatisfaction_Logic 要求 ReplyViewedAt 不為 null，
        ///    而進入檢視頁時 MarkReplyViewedIfNeededAsync 已經記過了。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSatisfaction(string? token, int? bookingId, byte satisfaction)
        {
            if (satisfaction > 2)
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "輸入異常，請重試";
                return RedirectToAction(nameof(Index));
            }

            ReviewMain? review = null;
            object routeValues;

            if (!string.IsNullOrWhiteSpace(token))
            {
                var r = await ResolveVisitTicketAsync(token);
                if (r.State != EligState.AlreadyReviewed)
                    return RedirectToAction(nameof(Index));
                review = r.ExistingReview;
                routeValues = new { token = r.Ticket!.Qrtoken };
            }
            else
            {
                var r = await ResolveBookingTicketAsync(bookingId);
                if (r.State != EligState.AlreadyReviewed)
                    return RedirectToAction(nameof(Index));
                review = r.ExistingReview;
                routeValues = new { bookingId = r.Ticket!.ReviewPerBookingId };
            }

            // 沒有回覆就沒有滿意度可言；已表態過不給改（按鈕本來就不會出現）
            if (review!.RepliedAt != null && review.ReplySatisfaction == null)
            {
                review.ReplyViewedAt ??= _timeService.Now;   // 保險，正常已經有值
                review.ReplySatisfaction = satisfaction;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(ShowMyReviewPage), routeValues);
        }

        // MarkReplyViewed 不再需要獨立的 Action——
        // 進入檢視頁時 MarkReplyViewedIfNeededAsync 就記錄了。
        // 之後若改成 AJAX 局部載入，再把它拿出來當端點。

        [HttpGet]
        public IActionResult Mine()             // 我的評論清單（會員）
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        public IActionResult CheckMyClaims()
        {
            // 檢查有沒有任何一筆 Claim 的型別是「角色」
            var roles = User.Claims
                            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToList();

            // 可以在這裡打斷點（Breakpoint），看 roles 陣列裡面有沒有字串（例如 "Member", "Admin"）
            return Json(roles);
        }
    }
}
