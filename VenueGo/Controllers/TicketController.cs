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
        public IActionResult Index(string? txtKeyword)
        {
            List<EntryTicketListViewModel> datas = new List<EntryTicketListViewModel>();
            string keyWord = txtKeyword ?? string.Empty;
            if (string.IsNullOrEmpty(keyWord))
            {
                datas = (new CTicketViewModelFactory()).TodayAllTicketList();
            }
            else
            {
                datas = (new CTicketViewModelFactory()).SearchByKeyword(keyWord);
            }
            return View(datas);
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
