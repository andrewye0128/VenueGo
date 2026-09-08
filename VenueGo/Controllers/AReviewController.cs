using Microsoft.AspNetCore.Mvc;

namespace VenueGo.Controllers
{
    public class AReviewController : Controller
    {
        [HttpGet]
        public IActionResult Index() // 佇列清單
        {
            return View();
        }

        [HttpPost]
        public IActionResult MarkRead() // 標記已讀
        {
            return View();
        }

        [HttpPost]
        public IActionResult TogglePin() // 置頂
        {
            return View();
        }

        [HttpPost]
        public IActionResult Reply() // 送出回覆
        {
            return View();
        }

        [HttpPost]
        public IActionResult MarkSpam() // 標記垃圾
        {
            return View();
        }

    }
}
