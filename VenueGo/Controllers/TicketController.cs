using Microsoft.AspNetCore.Mvc;
using VenueGo.Data;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.CheckinViewModels;
using VenueGo.Services.CheckIn;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace VenueGo.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        private readonly ICheckInService _checkInService;

        private int? GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }

        public TicketController(ICheckInService checkInService)
        {
            _checkInService = checkInService;
        }

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

        public async Task<IActionResult> Detail(int id)
        {
            await _checkInService.SettleTicketAsync(id);   // 進頁面先結算，狀態才是最新的
            var vm = (new CTicketViewModelFactory()).GetTicketDetail(id);
            if (vm == null) return NotFound();
            return View(vm);
        }


        private static string GetFailMessage(CheckInFailReason reason, string actionText) => reason switch
        {
            CheckInFailReason.TicketNotFound => "找不到這張票券",
            CheckInFailReason.AlreadyCancelled => $"票券已取消，無法{actionText}",
            CheckInFailReason.AlreadyExpired => $"票券已失效，無法{actionText}",
            CheckInFailReason.AlreadyCompleted => $"票券已使用完畢，無法{actionText}",
            CheckInFailReason.NotYetStartTime => $"尚未到預約時間，無法{actionText}",
            CheckInFailReason.InvalidSequence => actionText switch 
            {
                "入場" => "入場順序異常（可能已經入場）",
                "離場" => "離場順序異常（這張票還沒入場）",
                "取消" => "只有「有效」狀態的票券才能取消（已使用過的不行）",
                "轉失效" => "只有「有效」狀態的票券才能轉失效",
                _ => $"{actionText}失敗：異常操作行為"
            },
            _ => $"{actionText}失敗"
        };

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualCheckIn(int id, string? remark)
        {
            // 這裡先撈userId -> 之後要改為employeeId
            var userId = GetCurrentUserId();
            if (userId is null)
                return Unauthorized();   // 沒登入就不能執行後台操作
            
            var result = await _checkInService.CheckInAsync(id, userId, isManualOverride: true);

            if(result.Success)
            {
                TempData["SuccessMessage"] = "手動入場成功";
            }
            else
            {
                TempData["ErrorMessage"] = GetFailMessage(result.Reason!.Value, "入場"); // 失敗一定有值
            }
            return RedirectToAction("Detail", new { id });
        }
        //public IActionResult ManualCheckIn(int id, string? remark)
        //{
        //    (new CTicketViewModelFactory()).ManualCheckIn(id, operatorId: 1);
        //    return RedirectToAction("Detail", new { id });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualCheckOut(int id, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var result = await _checkInService.CheckOutAsync(id, userId, isManualOverride: true);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "手動離場成功";
            }
            else
            {
                TempData["ErrorMessage"] = GetFailMessage(result.Reason!.Value, "離場"); // 失敗一定有值
            }
            return RedirectToAction("Detail", new { id });
        }
        //public IActionResult ManualCheckOut(int id, string? remark)
        //{
        //    (new CTicketViewModelFactory()).ManualCheckOut(id, operatorId: 1);
        //    return RedirectToAction("Detail", new { id });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualCancel(int id, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            // CancelAsync 的 operatorId 不是可為 null，所以用 userId.Value
            var result = await _checkInService.CancelAsync(id, userId.Value, isManualOverride: true);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "手動取消成功";
            }
            else
            {
                TempData["ErrorMessage"] = GetFailMessage(result.Reason!.Value, "取消"); // 失敗一定有值
            }
            return RedirectToAction("Detail", new { id });
        }
        //public IActionResult ManualCancel(int id, string? remark)
        //{
        //    (new CTicketViewModelFactory()).ManualCancel(id, operatorId: 1);
        //    return RedirectToAction("Detail", new { id });
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualExpire(int id, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var result = await _checkInService.ExpireAsync(id, userId.Value, isManualOverride: true);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "手動轉失效成功";
            }
            else
            {
                TempData["ErrorMessage"] = GetFailMessage(result.Reason!.Value, "轉失效"); // 失敗一定有值
            }
            return RedirectToAction("Detail", new { id });
        }
        //public IActionResult ManualExpire(int id, string? remark)
        //{
        //    (new CTicketViewModelFactory()).ManualExpire(id, operatorId: 1);
        //    return RedirectToAction("Detail", new { id });
        //}
    }
}