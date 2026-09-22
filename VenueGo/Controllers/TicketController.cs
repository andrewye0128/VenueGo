using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.CheckinViewModels;

namespace VenueGo.Controllers
{
    public class TicketController : Controller
    {
        public IActionResult Index(string? txtKeyword, DateOnly? selectedDate, int? venueId, int? status)
        {

            // 沒有選擇日期時，預設使用今天的日期, 第一次進入頁面時，selectedDate 為 null傳入今天
            // ViewBag傳到頁面是今天, 前一天為今天 - 1, 後一天為今天 + 1
            // 有選擇日期時，使用選擇的日期, selectedDate 為選擇的日期
            // ViewBag傳到頁面是選擇的日期, 前一天為選擇的日期 - 1, 後一天為選擇的日期 + 1

            //防呆
            DateOnly targetDate = selectedDate ?? DateOnly.FromDateTime(DateTime.Now);

            var vm = new TicketIndexViewModel
            {
                SelectedDate = targetDate,
                Keyword = txtKeyword,
                SelectedVenueId = venueId,
                SelectedStatus = status,
                // 使用<SelectListItem>接收下拉選單內容並用asp-items綁定了CTicketViewModelFactory裡搜尋場地的結果
                AvailableVenues = (new CTicketViewModelFactory()).GetVenueOptions(),
                Tickets = (new CTicketViewModelFactory()).SearchTickets(targetDate, txtKeyword, venueId, status)
            };

            return View(vm);
        }

        // 快速報到:只處理 Valid(1) → Used(2)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult QuickCheckIn(int id)
        {
            (new CTicketViewModelFactory()).UpdateTicketStatus(id);

            return RedirectToAction("Index");
        }

        public IActionResult Detail(int id)
        {
            var vm = (new CTicketViewModelFactory()).GetTicketDetail(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManualCheckIn(int id, string? remark)
        {
            (new CTicketViewModelFactory()).ManualCheckIn(id, operatorId: 1);
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManualCheckOut(int id, string? remark)
        {
            (new CTicketViewModelFactory()).ManualCheckOut(id, operatorId: 1);
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManualCancel(int id, string? remark)
        {
            (new CTicketViewModelFactory()).ManualCancel(id, operatorId: 1);
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ManualExpire(int id, string? remark)
        {
            (new CTicketViewModelFactory()).ManualExpire(id, operatorId: 1);
            return RedirectToAction("Detail", new { id });
        }
    }
}