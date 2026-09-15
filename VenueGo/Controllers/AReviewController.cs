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
            throw new NotImplementedException();
        }

        [HttpPost]
        public IActionResult TogglePin() // 置頂
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public IActionResult Reply() // 送出回覆
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public IActionResult MarkSpam() // 標記垃圾
        {
            throw new NotImplementedException();
        }

    }
}
