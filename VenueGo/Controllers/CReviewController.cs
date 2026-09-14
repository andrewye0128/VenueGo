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
        // 將注入的 db 指派給私有唯讀欄位
        private readonly dbVenueContext _db = db;
        private readonly ICurrentUser _currentUser = currentUser;

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
        public IActionResult ShowMyReviewPage(string? token) // 評論查看頁面
        {
            if (token == null) 
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "載入時發生異常，請重試";
                return RedirectToAction("Index");
            }
            var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.Qrtoken == token.Trim());
            if (pVisit == null) // 沒查到評論資格
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction("Index");
            }
            var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == pVisit.ReviewPerVisitId);
            if (review == null)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無指定評論";
                return RedirectToAction("Index");
            }

            string? displayName = review.IsAnonymous ? review.AnonymousNickname : review.UserId.ToString();

            if (review.ReplyViewedAt == null)
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
            if (token == null) // 沒token
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "你忘記輸入囉～";
                return RedirectToAction("Index");
            }
            var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.Qrtoken == token.Trim());
            if (pVisit == null) // 沒查到評論資格
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "找不到你要的東西耶";
                return RedirectToAction("Index");
            }

            int pvid = pVisit.ReviewPerVisitId;
            var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == pvid);
            if (review != null) // 已寫過評論
            {
                return RedirectToAction("ShowMyReviewPage", new { token = pVisit.Qrtoken }); // 顯示已提交的評論
            }

            if (DateTime.Now >= pVisit.ExpiredAt) // 評論資格逾時
            {
                TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                return RedirectToAction("Index");
            }

            ReviewCreateForVisitVM vm = new ReviewCreateForVisitVM();
            vm.ReviewPerVisitId = pvid;
            vm.QrToken = token.Trim();
            vm.StarRating = null;
            vm.ReviewContent = null;
            vm.MentionsVenue = false;
            vm.MentionsStaff = false;
            vm.IsAnonymous = false; 
            vm.IsPublic = true; // 預設公開

            var venue = _db.Venues.FirstOrDefault(s => s.VenueId == pVisit.VenueId);
            vm.VenueName = venue?.VenueName; // venue 為null則回傳null
            vm.RentStartTime = pVisit.RentStartTime;

            return View(vm);
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
                return RedirectToAction("Index");
            }
            var review = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == vm.ReviewPerVisitId);
            if (review != null) // 已寫過評論
            {
                return RedirectToAction("ShowMyReviewPage", new { token = pVisit.Qrtoken }); // 顯示已提交的評論
            }
            if (DateTime.Now >= pVisit.ExpiredAt) // 評論資格逾時
            {
                TempData[CDictionary.TK_MSG_評論資格過期] = "超過可以評論的時間囉，下次請早";
                return RedirectToAction("Index");
            }

            int? userId = _currentUser.MemberId;
            if (userId == null)
                vm.IsAnonymous = true;

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

            return RedirectToAction("ShowMyReviewPage", new { token = pVisit.Qrtoken });
        }

        [HttpGet]
        public IActionResult CreateForBooking(int? id) // 填寫評論(預約)
        {
            return View();
        }
        [HttpPost]
        public IActionResult CreateForBooking() // 送出CreateForBooking
        {
            return View();
        }

        [HttpGet]
        public IActionResult Mine() // 查看我的評論(會員)
        {
            return View();
        }

        [HttpPost]
        public IActionResult SetVisibility() // 更改評論公開狀態
        {
            return View();
        }

        [HttpPost]
        public IActionResult MarkReplyViewed() // 紀錄查看回覆時間
        {
            return View();
        }

        [HttpPost]
        public IActionResult SetSatisfaction() // 表態對回覆的滿意度
        {
            return View();
        }
    }
}
