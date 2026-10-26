using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Constants;
using VenueGo.Dtos;
using VenueGo.Dtos.TicketDtos;
using VenueGo.Enums;
using VenueGo.Extensions;
using VenueGo.Models.CheckinModels;
using VenueGo.Services.CheckIn;
using VenueGo.Services.Ticket;
using VenueGo.ViewModels.CheckinViewModels;


namespace VenueGo.Controllers
{
    // 後台掃描驗票：櫃台用鏡頭掃會員的動態 QR，驗證後直接入場或離場
    [Authorize(Roles = RoleNames.BackOffice)]
    public class ScanController : Controller
    {
        private const string ModeIn = "in";
        private const string ModeOut = "out";

        private readonly QrTokenService _qrTokenService;
        private readonly ICheckInService _checkInService;
        private readonly CTicketViewModelFactory _viewFactory;

        public ScanController(QrTokenService qrTokenService, ICheckInService checkInService, CTicketViewModelFactory viewFactory)
        {
            _qrTokenService = qrTokenService;
            _checkInService = checkInService;
            _viewFactory = viewFactory;
        }


        // 掃描頁（5-5 會換成帶 ViewModel 的版本）
        public async Task<IActionResult> Index(int venueId)
        {
            string? venueName = await _viewFactory.GetActiveVenueName(venueId);
            if(venueName == null)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            var vm = new ScanViewModel
            {
                VerifyUrl = Url.Action(nameof(Verify), "Scan")!,
                VenueId = venueId,
                VenueName = venueName
            };
            return View(vm);
        }

        // 驗票：前端把掃到的 QR 和模式送來
        // POST /Scan/Verify，Body 是 JSON { payload, mode }，要帶防偽 token（標頭 RequestVerificationToken）
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify([FromBody] ScanVerifyRequest? request)
        {
            // 第一關：誰在操作、請求格式對不對
            int? operatorId = User.GetUserId();
            if (operatorId is null)
                return Unauthorized(ApiResult.Fail("請先登入", "UNAUTHORIZED"));

            if (request == null || (request.Mode != ModeIn && request.Mode != ModeOut))
                return BadRequest(ApiResult.Fail("掃描模式錯誤", "INVALID_MODE"));

            // 第二關：QR 是不是真的、有沒有過期
            QrVerifyResult verify = await _qrTokenService.VerifyAsync(request.Payload);
            if (!verify.Success)
            {
                // QR 不可信，不附任何票券資料，避免被拿來探測票券
                return Ok(new ScanVerifyResultDto
                {
                    Success = false,
                    Message = verify.Reason!.Value.GetDisplayName()   // 失敗一定有原因
                });
            }

            // 第三關: 查票券資料，確認是這個場館的票（QR 是真的，所以失敗時可以附票券資料）
            // 拿取驗證成功的QRcode裡的簽章結果
            int ticketId = verify.TicketId!.Value;
            ScanTicketSummaryDto? summary = await _viewFactory.GetScanSummaryAsync(ticketId);
            if (summary == null)
            {
                return Ok(new ScanVerifyResultDto
                {
                    Success = false,
                    Message = "查無此票券資訊"
                });
            }

            // 如果正在掃描的場地和票券場地不同
            if(summary.VenueId != request.VenueId)
            {
                return Ok(new ScanVerifyResultDto
                {
                    Success = false,
                    Message = $"此票為其他場館({summary.VenueName})，請改至該場館使用",
                    TicketSummary = summary
                });
            }

            // 第四關：真正入場或離場（票的狀態、時間、進出順序，仍由 CheckInService 判斷）
               // 成功一定有票號
            bool isCheckIn = request.Mode == ModeIn;
            string actionText = isCheckIn ? "入場" : "離場";

            CheckInResult result = isCheckIn
                ? await _checkInService.CheckInAsync(ticketId, operatorId, isManualOverride: false)
                : await _checkInService.CheckOutAsync(ticketId, operatorId, isManualOverride: false);

            // 組結果：不管成功或失敗，QR 是真的就附上票券資料，讓櫃台知道是誰的票
            var dto = new ScanVerifyResultDto
            {
                TicketSummary = summary
            };

            if (result.Success)
            {
                dto.Success = true;
                dto.Message = actionText + "成功";
            }

            else if (result.Reason == CheckInFailReason.ConfirmOvertimeCharge)
            {
                // 離場時已超過預約結束時間，要先收費才能離場：前端顯示「前往結帳」按鈕
                dto.Success = false;
                dto.Message = "已超過預約時間，離場前需先收取超時費用";
                dto.NeedOvertimeCheckout = true;
                dto.OvertimeCheckoutUrl = Url.Action("OvertimeCheckout", "Ticket", new { id = ticketId });
            }
            else
            {
                dto.Success = false;
                dto.Message = CheckinDisplayText.FailMessage(result.Reason!.Value, actionText);   // 失敗一定有值
            }

            return Ok(dto);
        }
    }
}