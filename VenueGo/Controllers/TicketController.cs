using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Services.CheckIn;
using VenueGo.ViewModels.CheckinViewModels;
using VenueGo.Services.Ticket;

namespace VenueGo.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        //private readonly dbVenueContext _db;
        private readonly ICheckInService _checkInService;
        private readonly ITicketManualService _ticketManualService;
        private readonly CTicketViewModelFactory _ticketFactory;

        private readonly CTicketStatusLogFactory _ticketStatusLogFactory;

        private int? GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }

        public TicketController(ICheckInService checkInService, ITicketManualService ticketManualService, CTicketViewModelFactory ticketFactory, CTicketStatusLogFactory ticketStatusLogFactory)
        {
            _checkInService = checkInService;
            _ticketManualService = ticketManualService;
            _ticketFactory = ticketFactory;
            _ticketStatusLogFactory = ticketStatusLogFactory;
        }

        public IActionResult Index(string? txtKeyword, DateOnly? selectedDate, int? venueId, int? status)
        {

            // 沒有選擇日期時，預設使用今天的日期, 第一次進入頁面時，selectedDate 為 null傳入今天
            // ViewBag傳到頁面是今天, 前一天為今天 - 1, 後一天為今天 + 1
            // 有選擇日期時，使用選擇的日期, selectedDate 為選擇的日期
            // ViewBag傳到頁面是選擇的日期, 前一天為選擇的日期 - 1, 後一天為選擇的日期 + 1

            // 網址有搜尋條件
            if(Request.QueryString.HasValue)
            {
                HttpContext.Session.SetString("searchQuery", Request.QueryString.Value!);
            }

            //防呆
            DateOnly targetDate = selectedDate ?? DateOnly.FromDateTime(DateTime.Now);       

            var vm = new TicketIndexViewModel
            {
                SelectedDate = targetDate,
                Keyword = txtKeyword,
                SelectedVenueId = venueId,
                SelectedStatus = status,
                // 使用<SelectListItem>接收下拉選單內容並用asp-items綁定了CTicketViewModelFactory裡搜尋場地的結果
                AvailableVenues = _ticketFactory.GetVenueOptions(),
                Tickets = _ticketFactory.SearchTickets(targetDate, txtKeyword, venueId, status)
            };

            return View(vm);
        }

        // 快速報到:只處理 Valid(1) → Used(2), 可以不管條件進出場可拿來測試
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCheckIn(int id)
        {
            bool isSuccess = await _ticketFactory.UpdateTicketInStatus(id);

            if (isSuccess)
            {
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "票券不存在或是錯誤的進出行為";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCheckOut(int id)
        {
            bool isSuccess = await _ticketFactory.UpdateTicketOutStatus(id);

            if (isSuccess)
            {
                return RedirectToAction("Index");
            }
            else
            {
                TempData["ErrorMessage"] = "票券不存在或是錯誤的進出行為";
                return RedirectToAction("Index");
            }
        }


        // 進入到 Detail 頁面 --> 總Detail頁面生成(Tab1球場資訊頁面) --> 選擇(Tab)產生該頁面
        public async Task<IActionResult> Detail(int id)
        {
            //await _checkInService.SettleTicketAsync(id);
            // 進頁面先結算，狀態才是最新的, 改為自動排程
            var vm = _ticketFactory.GetDetailTab(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        public IActionResult ScanLog(int id)
        {
            var vm = _ticketFactory.GetScanTab(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        // 分頁 3:異動紀錄(取消/轉失效 + 異動紀錄)
        public IActionResult ManualLog(int id)
        {
            var vm = _ticketFactory.GetManualTab(id);
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

            if (result.Success)
            {
                TempData[CDictionary.TK_MSG_操作成功] = "手動入場成功";
            }
            else
            {
                TempData[CDictionary.TK_MSG_操作失敗] = GetFailMessage(result.Reason!.Value, "入場"); // 失敗一定有值
            }
            return RedirectToAction("ScanLog", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualCheckOut(int id, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var result = await _checkInService.CheckOutAsync(id, userId, isManualOverride: true);
            if (result.Success)
            {
                TempData[CDictionary.TK_MSG_操作成功] = "手動離場成功";
            }
            else
            {
                TempData[CDictionary.TK_MSG_操作失敗] = GetFailMessage(result.Reason!.Value, "離場"); // 失敗一定有值
            }
            return RedirectToAction("ScanLog", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualCancel(int id, TicketManualLogReasonType? reasonType, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var error = _ticketStatusLogFactory.ValidateInput(TicketManualLogAction.Cancel, reasonType, remark);
            if (error != null)
            {
                TempData[CDictionary.TK_MSG_操作失敗] = error;
                return RedirectToAction("ManualLog", new { id });
            }

            var result = await _ticketManualService.CancelAsync(id, userId.Value, reasonType!.Value, remark);
            if (result.Success)
            {
                TempData[CDictionary.TK_MSG_操作成功] = "手動轉取消成功";
            }
            else
            {
                TempData[CDictionary.TK_MSG_操作失敗] = GetFailMessage(result.Reason!.Value, "取消"); // 失敗一定有值
            }
            return RedirectToAction("ManualLog", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualExpire(int id, TicketManualLogReasonType? reasonType, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            // 將驗證方法放入到人工異常的factory裡去做驗證
            var error = _ticketStatusLogFactory.ValidateInput(TicketManualLogAction.Expire,reasonType, remark);
            if (error != null)
            {
                TempData[CDictionary.TK_MSG_操作失敗] = error;
                return RedirectToAction("ManualLog", new { id });
            }

            var result = await _ticketManualService.ExpireAsync(id, userId.Value, reasonType!.Value, remark);
            if (result.Success)
            {
                TempData[CDictionary.TK_MSG_操作成功] = "手動轉失效成功";
            }
            else
            {
                TempData[CDictionary.TK_MSG_操作失敗] = GetFailMessage(result.Reason!.Value, "轉失效"); // 失敗一定有值
            }
            return RedirectToAction("ManualLog", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManualReissue(int id, TicketManualLogReasonType? reasonType, string? remark)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            // 將驗證方法放入到人工異常的factory裡去做驗證
            var error = _ticketStatusLogFactory.ValidateInput(TicketManualLogAction.Reissue, reasonType, remark);
            if (error != null)
            {
                TempData[CDictionary.TK_MSG_操作失敗] = error;
                return RedirectToAction("ManualLog", new { id });
            }

            var result = await _ticketManualService.ExpireAsync(id, userId.Value, reasonType!.Value, remark);
            if (result.Success)
            {
                TempData[CDictionary.TK_MSG_操作成功] = "手動補發QRcode成功";
            }
            else
            {
                TempData[CDictionary.TK_MSG_操作失敗] = GetFailMessage(result.Reason!.Value, "補發QRcode"); // 失敗一定有值
            }
            return RedirectToAction("ManualLog", new { id });
        }

        public async Task<IActionResult> QrImage(int id)
        {
            var qrtoken = await _ticketFactory.GetQrtokenAsync(id);
            if (qrtoken == null) return NotFound();

            var pngBytes = QrCodeHelper.GeneratePng(qrtoken);
            return File(pngBytes, "image/png");
        }

    }
}