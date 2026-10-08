using Microsoft.AspNetCore.Mvc;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Controllers
{
    /// <summary>
    /// 會員帳號管理 + 個人資料。
    /// <para>
    /// 【重構說明】原本這支 Controller 約 500 行，直接注入 dbVenueContext，
    /// 查詢、驗證、交易、稽核紀錄全混在 Action 裡，還複製了一份員工編號產生與角色查詢。
    /// 現在商業邏輯已搬進：
    /// <see cref="IMemberAccountService"/>（會員清單、停權、重設密碼、解鎖、匯出）、
    /// <see cref="IEmployeeAccountService"/>（會員升格為員工，與員工管理共用同一套規則）、
    /// <see cref="IUserProfileService"/>（個人資料）。
    /// 這裡只負責：呼叫 Service、依 <see cref="OperationOutcome"/> 決定回傳什麼、把錯誤放進 ModelState / TempData。
    /// 不要再把資料庫查詢或業務規則寫回來。
    /// </para>
    /// </summary>
    [EmployeeAuthorize()]
    public class MemberController : Controller
    {
        private readonly IMemberAccountService _memberService;
        private readonly IEmployeeAccountService _employeeService;
        private readonly IUserProfileService _profileService;
        private readonly ICurrentUserService _currentUser;
        private readonly ITimeService _timeService;

        public MemberController(
            IMemberAccountService memberService,
            IEmployeeAccountService employeeService,
            IUserProfileService profileService,
            ICurrentUserService currentUser,
            ITimeService timeService)
        {
            _memberService = memberService;
            _employeeService = employeeService;
            _profileService = profileService;
            _currentUser = currentUser;
            _timeService = timeService;
        }

        #region 1. 會員清單與管理

        // GET: Member/Index (列出擁有「會員角色」的使用者)
        public async Task<IActionResult> Index(string? keyword, string? status, int page = 1, int pageSize = 10)
        {
            var model = await _memberService.GetMemberListAsync(keyword, status, page, pageSize);
            return View(model);
        }

        // GET: Member/GetDetailJson/5 (Modal 詳細資料 API)
        [HttpGet]
        public async Task<IActionResult> GetDetailJson(int id)
        {
            var detail = await _memberService.GetMemberDetailAsync(id);
            if (detail == null) return NotFound();

            return Json(detail);
        }

        // POST: Member/UpdateMemberStatus (變更會員狀態，如停權/啟用)
        [EmployeeAuthorize(RoleNames.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberStatus(int userId, string status)
        {
            var result = await _memberService.UpdateMemberStatusAsync(userId, status, GetCurrentUserId());

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = $"已將會員 {result.Data} 狀態更新為：{status}";
            }
            else
            {
                TempData["ErrorMessage"] = DescribeFailure(result);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Member/ResetMemberPassword
        [EmployeeAuthorize(RoleNames.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetMemberPassword(int userId)
        {
            var result = await _memberService.ResetPasswordAsync(userId, GetCurrentUserId());

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] =
                    $"已將會員 {result.Data} 的密碼重置為預設密碼：{IMemberAccountService.DefaultResetPassword}";
            }
            else
            {
                TempData["ErrorMessage"] = DescribeFailure(result);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Member/UnlockAccount
        [EmployeeAuthorize(RoleNames.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockAccount(int userId)
        {
            var result = await _memberService.UnlockAccountAsync(userId, GetCurrentUserId());

            // [沿用原本行為] 查無資料回 404，其他失敗才用 TempData 導回清單
            if (result.Outcome == OperationOutcome.NotFound) return NotFound();

            if (result.IsSuccess)
            {
                TempData["SuccessMessage"] = $"已成功解鎖會員 {result.Data} 的帳號。";
            }
            else
            {
                TempData["ErrorMessage"] = DescribeFailure(result);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Member/ExportToCsv
        public async Task<IActionResult> ExportToCsv()
        {
            var buffer = await _memberService.ExportMembersCsvAsync();
            return File(buffer, "text/csv", $"Members_Export_{_timeService.Now:yyyyMMdd}.csv");
        }

        #endregion

        #region 2. 會員升格為員工

        // GET: Member/ConvertToEmployee/5
        [EmployeeAuthorize(RoleNames.Admin)]
        public async Task<IActionResult> ConvertToEmployee(int id)
        {
            var result = await _employeeService.GetConvertToEmployeeFormAsync(id);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    return View(result.Data);

                case OperationOutcome.NotFound:
                    return NotFound();

                default: // ValidationFailed（已經是員工）/ Error
                    TempData["ErrorMessage"] = DescribeFailure(result);
                    return RedirectToAction(nameof(Index));
            }
        }

        // POST: Member/ConvertToEmployee
        [EmployeeAuthorize(RoleNames.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToEmployee(ConvertEmployeeViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            // DataAnnotation 驗證先擋，業務規則交給 Service
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _employeeService.GetConvertibleRolesAsync();
                return View(model);
            }

            var result = await _employeeService.ConvertMemberToEmployeeAsync(model, currentUserId);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = $"成功將會員 {result.Data} 升格為員工！";
                    return RedirectToAction(nameof(Index));

                case OperationOutcome.NotFound:
                    TempData["ErrorMessage"] = "查無該會員資料。";
                    return RedirectToAction(nameof(Index));

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    break;

                default: // Error
                    ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "轉變員工過程發生錯誤。");
                    break;
            }

            model.AvailableRoles = await _employeeService.GetConvertibleRolesAsync();
            return View(model);
        }

        #endregion

        #region 3. 個人資料 (Profile)

        // GET: Member/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // [安全/穩定性] 原本用 Email Claim 去查人：
            // (1) 管理員改了員工 Email 之後，該員工 Cookie 裡的 Email 還是舊的，會查不到自己。
            // (2) 改用 UserId 才是唯一且不會變的識別。
            var userId = _currentUser.UserId;
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = await _profileService.GetProfileAsync(userId.Value);
            if (model == null)
            {
                return NotFound("找不到使用者資料");
            }

            return View(model);
        }

        // POST: Member/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            // [安全修正] 要改誰一律用「目前登入者」，不看表單隱藏欄位的 model.UserId。
            // 原本任何登入的員工只要改掉隱藏欄位，就能修改別人的姓名與電話。
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                await FillReadOnlyProfileFieldsAsync(model, currentUserId);
                return View(model);
            }

            var result = await _profileService.UpdateProfileAsync(currentUserId, model);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = "個人資料已成功更新！";
                    return RedirectToAction(nameof(Profile));

                case OperationOutcome.NotFound:
                    return NotFound();

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    break;

                default: // Error
                    ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "更新個人資料時發生錯誤，請稍後再試。");
                    break;
            }

            await FillReadOnlyProfileFieldsAsync(model, currentUserId);
            return View(model);
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 取得目前登入者的 UserId；取不到就拋例外，不 fallback 成 0
        /// （理由同 SettingController：避免保護邏輯被繞過、避免稽核紀錄寫入 UserId = 0）。
        /// </summary>
        private int GetCurrentUserId()
        {
            return _currentUser.UserId
                ?? throw new InvalidOperationException("無法取得目前登入使用者的識別碼。");
        }

        /// <summary>
        /// 個人資料頁重新顯示時，把「唯讀欄位」（員工編號、職稱、角色、Email）補回去。
        /// 這些欄位不是使用者可編輯的內容，表單送回來時是空的或可能被竄改，一律以資料庫為準。
        /// （原本驗證失敗時角色清單會整個消失。）
        /// </summary>
        private async Task FillReadOnlyProfileFieldsAsync(UserProfileViewModel model, int userId)
        {
            var fresh = await _profileService.GetProfileAsync(userId);
            if (fresh == null) return;

            model.UserId = fresh.UserId;
            model.Email = fresh.Email;
            model.EmployeeNo = fresh.EmployeeNo;
            model.JobTitle = fresh.JobTitle;
            model.Roles = fresh.Roles;
        }

        /// <summary>把 Service 失敗結果轉成要顯示在清單頁上的一行錯誤訊息。</summary>
        private static string DescribeFailure<T>(ServiceResult<T> result)
        {
            return result.Outcome switch
            {
                OperationOutcome.NotFound => "查無該會員資料。",
                OperationOutcome.ValidationFailed =>
                    result.Errors.Select(e => e.Message).FirstOrDefault() ?? "資料驗證失敗。",
                _ => result.ErrorMessage ?? "操作失敗，請稍後再試。"
            };
        }

        #endregion
    }
}