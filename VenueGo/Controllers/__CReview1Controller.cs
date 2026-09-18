using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Controllers
{
    public class CReview1Controller(dbVenueContext db, ICurrentUser currentUser) : Controller
    {
        // 將注入的 db 指派給私有唯讀欄位
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUser _currentUser = currentUser;

        // ════════════════════════════════════════════════════════
        //  第一區：資格判定
        //  這一區只回答「能不能評論」，不碰畫面、不組 VM、不寫資料。
        // ════════════════════════════════════════════════════════

        // 憑證的狀態。用 enum 而不是 bool，因為「不能評論」有三種原因，呼叫端要分別處理。
        private enum EligState { Ok, NotFound, Expired, AlreadyReviewed }

        // record 是「一次建好就不能改的小資料袋」，一行就等於一個有三個唯讀屬性的類別。
        private sealed record VisitTicket(  // 包含評論資格、憑證、已撰寫的評論
            EligState State,
            ReviewPerVisit? Ticket,
            ReviewMain? ExistingReview);

        private sealed record BookingTicket(
            EligState State,
            ReviewPerBooking? Ticket,
            ReviewMain? ExistingReview);

        // QRToken 驗證流程
        private VisitTicket ResolveVisitTicket(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return new(EligState.NotFound, null, null); // 未輸入❌

            var ticket = _db.ReviewPerVisits.FirstOrDefault(v => v.Qrtoken == token.Trim());
            if (ticket == null)
                return new(EligState.NotFound, null, null); // 查無評論資格❌

            var existing = _db.ReviewMains.FirstOrDefault(r => r.ReviewPerVisitId == ticket.ReviewPerVisitId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing); // 已評過📋

            if (DateTime.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null); // 已逾期❌

            return new(EligState.Ok, ticket, null); // 可以評論👌
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
                return new(EligState.NotFound, null, null); // 無效輸入❌

            var ticket = _db.ReviewPerBookings.FirstOrDefault(b => b.ReviewPerBookingId == id);
            if (ticket == null)
                return new(EligState.NotFound, null, null); // 查無評論資格❌

            var order = _db.Orders.FirstOrDefault(o => o.OrderId == ticket.OrderId);
            if (order == null)
                return new(EligState.NotFound, null, null); // 查無可評論訂單❌

            if (_currentUser.MemberId != ticket.UserId) 
                return new(EligState.NotFound, null, null); // 是你的評論嗎你就評❌

            var existing = _db.ReviewMains.FirstOrDefault(r => r.ReviewPerBookingId == ticket.ReviewPerBookingId);
            if (existing != null)
                return new(EligState.AlreadyReviewed, ticket, existing); // 已評過📋

            if (DateTime.Now >= ticket.ExpiredAt)
                return new(EligState.Expired, ticket, null); // 已逾期❌

            return new(EligState.Ok, ticket, null); // 可以評論👌
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

        [HttpGet]
        public IActionResult Index() // 評論專區
        {            
            return View();
        }
        //[HttpPost]
        //public IActionResult Index(string? token) 
        //{
        //    return RedirectToAction("CreateForVisit", new { token = token }); // 透過匿名物件，將 token 傳給下一個 Action
        //}

        [HttpGet]
        public IActionResult ShowMyReviewPage(string? token, string? orderNo) // 評論查看頁面
        {
            if (token == null)
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "載入時發生異常，請重試";
                return RedirectToAction(nameof(Index));
            }
            var visitResult = ResolveVisitTicket(token);

            switch (visitResult.State)
            {
                case EligState.NotFound:
                    TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                    return RedirectToAction(nameof(Index));

                case EligState.AlreadyReviewed:
                    return RedirectToAction(nameof(ShowMyReviewPage),
                                            new { token = visitResult.Ticket!.Qrtoken });

                case EligState.Expired:
                    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                    return RedirectToAction(nameof(Index));
            }


            //var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.Qrtoken == token.Trim());
            //if (pVisit == null) // 沒查到評論資格
            //{
            //    TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
            //    return RedirectToAction(nameof(Index));
            //}
            //var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == pVisit.ReviewPerVisitId);
            //if (review == null)
            //{
            //    TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
            //    return RedirectToAction(nameof(Index));
            //}

            var loginUser = _db.Users.FirstOrDefault(u => u.UserId == visitResult.Ticket.id)
            string? displayName = review.IsAnonymous ? review.AnonymousNickname : review.UserId.ToString();

            if (review.ReplyViewedAt == null && review.RepliedAt <= DateTime.Now)
            {
                review.ReplyViewedAt = DateTime.Now;
                _db.SaveChanges();
            }

            var vm = new MyReviewPageVM
            {
                ReviewId = review.ReviewId,
                Qrtoken = token.Trim(),
                StarRating = review.StarRating,
                ReviewContent = review.ReviewContent,
                IsAnonymous = review.IsAnonymous,
                IsPublic = review.IsPublic,
                MentionsVenue = review.MentionsVenue,
                MentionsStaff = review.MentionsStaff,
                CreatedAt = review.CreatedAt,
                DisplayName = displayName ?? "不知道是誰",
                ReplyContent = review.ReplyContent,
                RepliedAt = review.RepliedAt,
                ReplyViewedAt = review.ReplyViewedAt,
                ReplySatisfaction = review.ReplySatisfaction,
                IsSpamMarked = review.SpamMarkedAt != null
            };
            
            return View(vm);
        }

        [HttpGet]
        public IActionResult CreateForVisit(string? token) // 填寫評論(現場)
        {
            var ticketResult = ResolveVisitTicket(token);

            switch (ticketResult.State)
            {
                case EligState.NotFound:
                    TempData[CDictionary.TK_MSG_找不到指定物件] = "找不到你要的東西耶";
                    return RedirectToAction(nameof(Index));

                case EligState.AlreadyReviewed:
                    return RedirectToAction(nameof(ShowMyReviewPage),
                                            new { token = ticketResult.Ticket!.Qrtoken });

                case EligState.Expired:
                    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                    return RedirectToAction(nameof(Index));
            }

            // 到這裡 State 一定是 Ok，Ticket 一定不是 null
            var vm = BuildCreateVm(ticketResult.Ticket!);

            return View(vm);

            //if (token == null) // 沒token
            //{
            //    TempData[CDictionary.TK_MSG_Input錯誤] = "你忘記輸入囉～";
            //    return RedirectToAction(nameof(Index));
            //}
            //var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.Qrtoken == token.Trim());
            //if (pVisit == null) // 沒查到評論資格
            //{
            //    TempData[CDictionary.TK_MSG_找不到指定物件] = "找不到你要的東西耶";
            //    return RedirectToAction(nameof(Index));
            //}

            //int pvid = pVisit.ReviewPerVisitId;
            //var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == pvid);
            //if (review != null) // 已寫過評論
            //{
            //    return RedirectToAction(nameof(ShowMyReviewPage), new { token = pVisit.Qrtoken }); // 顯示已提交的評論
            //}

            //if (DateTime.Now >= pVisit.ExpiredAt) // 評論資格逾時
            //{
            //    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
            //    return RedirectToAction(nameof(Index));
            //}

            //ReviewCreateForVisitVM vm = new ReviewCreateForVisitVM();
            //vm.ReviewPerVisitId = pvid;
            //vm.QrToken = token.Trim();
            //vm.StarRating = null;
            //vm.ReviewContent = null;
            //vm.MentionsVenue = false;
            //vm.MentionsStaff = false;
            //vm.CanChooseAnonymous = _currentUser.MemberId != null;
            //vm.IsAnonymous = !vm.CanChooseAnonymous;   // 未登入 → 鎖定匿名並顯示為開啟
            ////vm.IsPublic = true; // 父類別已預設公開

            //var venue = _db.Venues.FirstOrDefault(s => s.VenueId == pVisit.VenueId);
            //vm.VenueName = venue?.VenueName; // venue 為null則回傳null
            //vm.RentStartTime = pVisit.RentStartTime;

        }

        private ReviewCreateForVisitVM BuildCreateVm(ReviewPerVisit perVisit)
        {
            //vm.StarRating = null;
            //vm.ReviewContent = null;
            //vm.MentionsVenue = false;
            //vm.MentionsStaff = false;
            //vm.CanChooseAnonymous = _currentUser.MemberId != null;
            //vm.IsAnonymous = !vm.CanChooseAnonymous;   // 未登入 → 鎖定匿名並顯示為開啟
            ////vm.IsPublic = true; // 父類別已預設公開

            var venue = _db.Venues.FirstOrDefault(n => n.VenueId == perVisit.VenueId);

            var vm = new ReviewCreateForVisitVM
            {
                ReviewPerVisitId = perVisit.ReviewPerVisitId,
                QrToken = perVisit.Qrtoken,
                VenueName = venue?.VenueName ?? "場地資料異常",
                RentStartTime = perVisit.RentStartTime,

                StarRating = null,
                ReviewContent = null,
                MentionsVenue = false,
                MentionsStaff = false,
                CanChooseAnonymous = _currentUser.MemberId != null,
                IsAnonymous = _currentUser.MemberId == null,   // 未登入 → 鎖定匿名並顯示為開啟
                IsPublic = true // 父類別已預設公開
            };

            return vm;
        }
        private ReviewCreateForBookingVM BuildCreateVm(ReviewPerBooking perBooking)
        {
            //vm.StarRating = null;
            //vm.ReviewContent = null;
            //vm.CanChooseAnonymous = _currentUser.MemberId != null;
            //IsAnonymous = _currentUser.MemberId == null,   // 未登入 → 鎖定匿名並顯示為開啟
            //IsPublic = true // 父類別已預設公開

            var order = _db.Orders.FirstOrDefault(o => o.OrderId == perBooking.OrderId);

            var vm = new ReviewCreateForBookingVM
            {
                ReviewPerBookingId = perBooking.ReviewPerBookingId,
                OrderId = perBooking.OrderId,
                PaymentMethod = perBooking.PaymentMethod,
                OrderNo = order!.OrderNo,

                StarRating = null,
                ReviewContent = null,
            };

            return vm;
        }


        [HttpPost]
        public IActionResult CreateForVisit(ReviewCreateForVisitVM vm, string? token) // 送出CreateForVisit
        {
            if (!ModelState.IsValid)
            {
                return View(vm); // 驗證失敗，返回原頁面並顯示錯誤
            }
            var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.ReviewPerVisitId == vm.ReviewPerVisitId && s.Qrtoken == token);
            if (pVisit == null) // 雙重驗證不通過
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "輸入異常，請重試";
                return RedirectToAction(nameof(Index));
            }
            var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == vm.ReviewPerVisitId);
            if (review != null) // 已寫過評論
            {
                return RedirectToAction(nameof(ShowMyReviewPage), new { token = pVisit.Qrtoken }); // 顯示已提交的評論
            }
            if (DateTime.Now >= pVisit.ExpiredAt) // 評論資格逾時
            {
                TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                return RedirectToAction(nameof(Index));
            }

            int? userId = _currentUser.MemberId;
            if (userId == null)
                vm.IsAnonymous = true; // 前端的 disabled 只是不讓人點，繞過表單直接送請求還是送得進來

            // 只有匿名才產生暱稱，實名留 null（= 用會員真名）
            string? nickname = vm.IsAnonymous
                                 ? NicknameGenerator.Generate()
                                 : null;

            var newReview = new ReviewMain
            {
                ReviewPerVisitId = vm.ReviewPerVisitId, // 已雙重驗證
                ReviewPerBookingId = null,          // XOR：現場評論這欄必為 null
                UserId = userId,
                StarRating = vm.StarRating!.Value, // ! 保證StarRating不為null
                ReviewContent = string.IsNullOrWhiteSpace(vm.ReviewContent)
                          ? null        // 純空白要存 null，
                          : vm.ReviewContent.Trim(),   // 否則撞 CHK_..._Content_NotBlank
                IsAnonymous = vm.IsAnonymous,
                IsPublic = vm.IsPublic,
                MentionsVenue = vm.MentionsVenue,
                MentionsStaff = vm.MentionsStaff,
                AnonymousNickname = nickname,
                CreatedAt = DateTime.Now
            };
            /*
                ReviewMain newReview = new ReviewMain();
                newReview.ReviewPerBookingId = null;
                newReview.ReviewPerVisitId = vm.ReviewPerVisitId;
                newReview.UserId = null;
                if (newReview.UserId == null)
                    vm.IsAnonymous = true;
                newReview.StarRating = (byte)vm.StarRating;
                newReview.ReplyContent = vm.ReviewContent;
                newReview.IsAnonymous = vm.IsAnonymous;
                newReview.IsPublic = vm.IsPublic;
                newReview.MentionsVenue = vm.MentionsVenue;
                newReview.MentionsStaff = vm.MentionsStaff;
                newReview.CreatedAt = DateTime.Now;
                newReview.AnonymousNickname = NicknameGenerator.Generate();
            */

            _db.ReviewMains.Add(newReview);
            _db.SaveChanges(); // 別忘記儲存

            return RedirectToAction(nameof(ShowMyReviewPage), new { token = pVisit.Qrtoken });
        }

        [HttpGet]
        public IActionResult CreateForBooking(int? id) // 填寫評論(預約)
        {
            var ticketResult = ResolveBookingTicket(id);

            switch (ticketResult.State)
            {
                case EligState.NotFound:
                    TempData[CDictionary.TK_MSG_找不到指定物件] = "找不到你要的東西耶";
                    return RedirectToAction(nameof(Index));

                case EligState.AlreadyReviewed:
                    var order = _db.Orders.FirstOrDefault(o => o.OrderId == ticketResult.Ticket.OrderId);
                    return RedirectToAction(nameof(ShowMyReviewPage),
                                            new { orderNo = order.OrderNo });

                case EligState.Expired:
                    TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                    return RedirectToAction(nameof(Index));
            }

            // 到這裡 State 一定是 Ok，Ticket 一定不是 null
            var vm = BuildCreateVm(ticketResult.Ticket);

            return View(vm);
        }
        [HttpPost]
        public IActionResult CreateForBooking() // 送出CreateForBooking
        {
            
            throw new NotImplementedException();
        }

        [HttpGet]
        public IActionResult Mine() // 查看我的評論(會員)
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public IActionResult SetVisibility(string? token) // 更改評論公開狀態
        {
            var ticketResult = ResolveVisitTicket(token);
            if (ticketResult.State != EligState.AlreadyReviewed)
                return RedirectToAction(nameof(Index));
            
            ticketResult.ExistingReview!.IsPublic = !ticketResult.ExistingReview.IsPublic;
            _db.SaveChanges(); // 又忘了存
            
            return RedirectToAction(nameof(ShowMyReviewPage), new { token = ticketResult.Ticket!.Qrtoken });
        }

        [HttpPost]
        public IActionResult MarkReplyViewed() // 紀錄查看回覆時間
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public IActionResult SetSatisfaction() // 表態對回覆的滿意度
        {
            throw new NotImplementedException();
        }
    }
}
