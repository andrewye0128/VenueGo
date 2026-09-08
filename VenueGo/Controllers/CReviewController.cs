using Microsoft.AspNetCore.Mvc;
using VenueGo.Models.Entities;

namespace VenueGo.Controllers
{
    public class CReviewController : Controller
    {
        [HttpGet]
        public IActionResult Index() // 評論專區
        {
            return View();
        }

        [HttpGet]
        public IActionResult CreateForVisit(int? id) // 填寫評論(現場)
        {
            return View();
        }
        [HttpPost]
        public IActionResult CreateForVisit() // 送出CreateForVisit
        {
            return View();
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
