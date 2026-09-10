using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.ReviewVM;

namespace VenueGo.Controllers
{
    public class CReviewController(dbVenueContext db) : Controller
    {
        // 將注入的 db 指派給私有唯讀欄位
        private readonly dbVenueContext _db = db;
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
        public IActionResult ShowPublicReview(int? id) // 做成ReviewCardVM了
        {
            return View();
        }

        [HttpGet]
        public IActionResult CreateForVisit(string? token) // 填寫評論(現場)
        {
            if (token == null) // 沒token
            {
                TempData[CDictionary.TK_MSG_Input錯誤] = "你忘記輸入囉～";
                return RedirectToAction("Index");
            }
            var pVisit = _db.ReviewPerVisits.FirstOrDefault(s => s.Qrtoken == token);
            if (pVisit == null) // 沒查到評論資格
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "找不到你要的東西耶";
                return RedirectToAction("Index");
            }
            int pvid = pVisit.ReviewPerVisitId;
            var rv = _db.ReviewMains.FirstOrDefault(s => s.ReviewPerVisitId == pvid);
            if (rv != null) // 已寫過評論
            {
                return RedirectToAction("ShowPublicReview");
            }
            ReviewCreateInputVM vm = new ReviewCreateInputVM();
            vm.ReviewPerVisitId = pvid;
            vm.QrToken = token;
            vm.ReservationId = null;
            vm.StarRating = null;
            vm.ReviewContent = null;
            vm.MentionsVenue = false;
            vm.MentionsStaff = false;
            vm.IsAnonymous = true; // 先強制匿名
            vm.IsPublic = true; // 預設公開

            var venue = _db.Venues.FirstOrDefault(s => s.VenueId == pVisit.VenueId);
            if (venue == null)
                vm.VenueName = null;
            vm.VenueName = venue.VenueName;
            vm.RentStartTime = pVisit.RentStartTime;

            return View(vm);
        }
        [HttpPost]
        public IActionResult CreateForVisit(ReviewCreateInputVM vm) // 送出CreateForVisit
        {
            int? userId = null; // _currentUser.MemberId
            if (userId == null)
                vm.IsAnonymous = true;

            // 只有匿名才產生暱稱，實名留 null（= 用會員真名）
            string? nickname = vm.IsAnonymous
                                 ? NicknameGenerator.Generate()
                                 : null;

            var review = new ReviewMain
            {
                ReviewPerVisitId = vm.ReviewPerVisitId,
                ReviewPerBookingId = null,          // XOR：現場評論這欄必為 null
                UserId = userId,
                StarRating = vm.StarRating!.Value,
                ReviewContent = string.IsNullOrWhiteSpace(vm.ReviewContent)
                          ? null        // 純空白要存 null，
                          : vm.ReviewContent,   // 否則撞 CHK_..._Content_NotBlank
                IsAnonymous = vm.IsAnonymous,
                IsPublic = vm.IsPublic,
                MentionsVenue = vm.MentionsVenue,
                MentionsStaff = vm.MentionsStaff,
                AnonymousNickname = nickname,
                CreatedAt = DateTime.Now
            };

            //ReviewMain rv = new ReviewMain();
            //rv.ReviewPerBookingId = null;
            //rv.ReviewPerVisitId = vm.ReviewPerVisitId;
            //rv.UserId = null;
            //if (rv.UserId == null)
            //    vm.IsAnonymous = true;
            //rv.StarRating = (byte)vm.StarRating;
            //rv.ReplyContent = vm.ReviewContent;
            //rv.IsAnonymous = vm.IsAnonymous;
            //rv.IsPublic = vm.IsPublic;
            //rv.MentionsVenue = vm.MentionsVenue;
            //rv.MentionsStaff= vm.MentionsStaff;
            //rv.CreatedAt = DateTime.Now;
            //rv.AnonymousNickname = NicknameGenerator.Generate();

            _db.ReviewMains.Add(review);

            return RedirectToAction("Index");
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
