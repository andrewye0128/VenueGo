using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Controllers
{
    public class CReviewController(dbVenueContext db, ICurrentUser currentUser) : Controller
    {
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUser _currentUser = currentUser;

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
        private VisitTicket ResolveVisitTicket(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new(EligState.NotFound, null, null);          // 未輸入 ❌

            var ticket = _db.ReviewPerVisits
                            .FirstOrDefault(v => v.Qrtoken == token.Trim());
            if (ticket == null)
                return new(EligState.NotFound, null, null);          // 查無憑證 ❌

            var existing = _db.ReviewMains
                              .FirstOrDefault(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);  // 已評過 📋

            if (DateTime.Now >= ticket.ExpiredAt)
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
        private BookingTicket ResolveBookingTicket(int? id)
        {
            if (id == null || id <= 0)
                return new(EligState.NotFound, null, null);          // 無效輸入 ❌

            var ticket = _db.ReviewPerBookings
                            .FirstOrDefault(b => b.ReviewPerBookingId == id);
            if (ticket == null)
                return new(EligState.NotFound, null, null);          // 查無憑證 ❌

            // TODO: 登入方提供驗證方法後換掉這一行
            if (_currentUser.MemberId != ticket.UserId)
                return new(EligState.NotFound, null, null);          // 不是你的 ❌

            var existing = _db.ReviewMains
                              .FirstOrDefault(r => r.ReviewPerBookingId == ticket.ReviewPerBookingId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing);  // 已評過 📋

            if (DateTime.Now >= ticket.ExpiredAt)
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

        private ReviewCreateForVisitVM BuildCreateVm(ReviewPerVisit perVisit)
        {
            var venue = _db.Venues.FirstOrDefault(v => v.VenueId == perVisit.VenueId);

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
                CanChooseAnonymous = _currentUser.MemberId != null,
                IsAnonymous = _currentUser.MemberId == null,  // 未登入 → 鎖定匿名
                IsPublic = true
            };
        }

        private ReviewCreateForBookingVM BuildCreateVm(ReviewPerBooking perBooking)
        {
            var order = _db.Orders.FirstOrDefault(o => o.OrderId == perBooking.OrderId);

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
        private string ResolveDisplayName(ReviewMain review)
        {
            if (review.IsAnonymous)
                return review.AnonymousNickname ?? "匿名使用者";

            var user = _db.Users.FirstOrDefault(u => u.UserId == review.UserId);
            return user?.Name ?? "已停用的帳號";
        }

        /// <summary>
        /// 檢視頁的 VM。兩種評論共用同一個版面，差別只在
        /// 「用什麼識別自己」與「頁尾顯示什麼」。
        /// </summary>
        private MyReviewPageVM BuildMyReviewVm(
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
                DisplayName = ResolveDisplayName(review),

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

        // ════════════════════════════════════════════════════════
        //  第三區：寫入
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// 由輸入 VM 組出要存的評論實體。兩種評論共用。
        /// visitId 與 bookingId 一定只有一個有值（XOR 約束）。
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
                CreatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// 顧客打開檢視頁就代表看到回覆了。
        /// ⚠️ 只在「有回覆且尚未記錄」時寫，否則每次重新整理都會蓋掉原本的時間。
        ///    約束 ReplyViewed_Logic 也要求 RepliedAt 不為 null 且 ReplyViewedAt >= RepliedAt。
        /// </summary>
        private void MarkReplyViewedIfNeeded(ReviewMain review)
        {
            if (review.RepliedAt == null) return;
            if (review.ReplyViewedAt != null) return;

            review.ReplyViewedAt = DateTime.Now;
            _db.SaveChanges();
        }

        // ════════════════════════════════════════════════════════
        //  第四區：Action
        //  到這裡每個 Action 都只剩「接參數 → 呼叫 → 決定回什麼畫面」。
        // ════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Index()            // 評論專區
        {
            return View();
        }

        // ── 現場評論撰寫 ────────────────────────────────────

        [HttpGet]
        public IActionResult CreateForVisit(string? token)
        {
            var r = ResolveVisitTicket(token);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            return View(BuildCreateVm(r.Ticket!));
        }

        [HttpPost]
        public IActionResult CreateForVisit(ReviewCreateForVisitVM vm, string? token)
        {
            // ⚠️ 資格要重驗一次。表單可能停在頁面上好幾天，
            //    也可能有人繞過畫面直接送請求。
            var r = ResolveVisitTicket(token);
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
                var redo = BuildCreateVm(r.Ticket);
                redo.StarRating = vm.StarRating;
                redo.ReviewContent = vm.ReviewContent;
                redo.MentionsVenue = vm.MentionsVenue;
                redo.MentionsStaff = vm.MentionsStaff;
                redo.IsAnonymous = vm.IsAnonymous;
                redo.IsPublic = vm.IsPublic;
                return View(redo);
            }

            int? userId = _currentUser.MemberId;
            if (userId == null)
                vm.IsAnonymous = true;   // 前端 disabled 擋不住直接送請求的人

            var newReview = BuildNewReview(vm, r.Ticket.ReviewPerVisitId, null, userId);

            _db.ReviewMains.Add(newReview);
            _db.SaveChanges();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { token = r.Ticket.Qrtoken });
        }

        // ── 預約評論撰寫 ────────────────────────────────────

        [HttpGet]
        public IActionResult CreateForBooking(int? id)
        {
            var r = ResolveBookingTicket(id);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            return View(BuildCreateVm(r.Ticket!));
        }

        [HttpPost]
        public IActionResult CreateForBooking(ReviewCreateForBookingVM vm, int? id)
        {
            var r = ResolveBookingTicket(id);
            var reject = RejectIfNotOk(r);
            if (reject != null) return reject;

            if (r.Ticket!.ReviewPerBookingId != vm.ReviewPerBookingId)
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "輸入異常，請重試";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                var redo = BuildCreateVm(r.Ticket);
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
                                           _currentUser.MemberId);

            _db.ReviewMains.Add(newReview);
            _db.SaveChanges();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { bookingId = r.Ticket.ReviewPerBookingId });
        }

        // ── 檢視頁 ──────────────────────────────────────────

        /// <summary>
        /// 兩種評論共用一個頁面。token 有值走現場、bookingId 有值走預約。
        /// </summary>
        [HttpGet]
        public IActionResult ShowMyReviewPage(string? token, int? bookingId)
        {
            if (!string.IsNullOrWhiteSpace(token))
                return ShowVisitReview(token);

            if (bookingId != null)
                return ShowBookingReview(bookingId);

            TempData[CDictionary.TK_MSG_Input錯誤] = "載入時發生異常，請重試";
            return RedirectToAction(nameof(Index));
        }

        private IActionResult ShowVisitReview(string token)
        {
            var r = ResolveVisitTicket(token);

            // 這一頁要的是「已經評過」，跟撰寫頁剛好相反，所以不能用 RejectIfNotOk
            if (r.State != EligState.AlreadyReviewed)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction(nameof(Index));
            }

            var review = r.ExistingReview!;
            MarkReplyViewedIfNeeded(review);

            var venue = _db.Venues.FirstOrDefault(v => v.VenueId == r.Ticket!.VenueId);

            var vm = BuildMyReviewVm(review,
                                     token: r.Ticket!.Qrtoken,
                                     bookingId: null,
                                     venueName: venue?.VenueName,
                                     rentStartTime: r.Ticket.RentStartTime,
                                     orderNo: null);
            return View(nameof(ShowMyReviewPage), vm);
        }

        private IActionResult ShowBookingReview(int? bookingId)
        {
            var r = ResolveBookingTicket(bookingId);

            if (r.State != EligState.AlreadyReviewed)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction(nameof(Index));
            }

            var review = r.ExistingReview!;
            MarkReplyViewedIfNeeded(review);

            var order = _db.Orders.FirstOrDefault(o => o.OrderId == r.Ticket!.OrderId);

            var vm = BuildMyReviewVm(review,
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
        [HttpPost]
        public IActionResult SetVisibility(string? token, bool isPublic)
        {
            var r = ResolveVisitTicket(token);
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
            _db.SaveChanges();

            return RedirectToAction(nameof(ShowMyReviewPage),
                                    new { token = r.Ticket!.Qrtoken });
        }

        /// <summary>
        /// 對館方回覆表態。0 不滿意 / 1 普通 / 2 滿意。
        /// ⚠️ 約束 ReplySatisfaction_Logic 要求 ReplyViewedAt 不為 null，
        ///    而進入檢視頁時 MarkReplyViewedIfNeeded 已經記過了。
        /// </summary>
        [HttpPost]
        public IActionResult SetSatisfaction(string? token, int? bookingId, byte satisfaction)
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
                var r = ResolveVisitTicket(token);
                if (r.State != EligState.AlreadyReviewed)
                    return RedirectToAction(nameof(Index));
                review = r.ExistingReview;
                routeValues = new { token = r.Ticket!.Qrtoken };
            }
            else
            {
                var r = ResolveBookingTicket(bookingId);
                if (r.State != EligState.AlreadyReviewed)
                    return RedirectToAction(nameof(Index));
                review = r.ExistingReview;
                routeValues = new { bookingId = r.Ticket!.ReviewPerBookingId };
            }

            // 沒有回覆就沒有滿意度可言；已表態過不給改（按鈕本來就不會出現）
            if (review!.RepliedAt != null && review.ReplySatisfaction == null)
            {
                review.ReplyViewedAt ??= DateTime.Now;   // 保險，正常已經有值
                review.ReplySatisfaction = satisfaction;
                _db.SaveChanges();
            }

            return RedirectToAction(nameof(ShowMyReviewPage), routeValues);
        }

        // MarkReplyViewed 不再需要獨立的 Action——
        // 進入檢視頁時 MarkReplyViewedIfNeeded 就記錄了。
        // 之後若改成 AJAX 局部載入，再把它拿出來當端點。

        [HttpGet]
        public IActionResult Mine()             // 我的評論清單（會員）
        {
            throw new NotImplementedException();
        }
    }
}