using Microsoft.AspNetCore.Mvc;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Controllers
{
    /// <summary>
    /// 設定頁面：角色權限管理 + 員工帳號管理。
    /// <para>
    /// 【重構說明】原本這支 Controller 有 1100+ 行，角色/員工的驗證規則、
    /// 資料庫交易、稽核紀錄全部混在 Action 方法裡。現在商業邏輯已經搬進：
    /// <see cref="IRoleManagementService"/>（角色管理）、
    /// <see cref="IEmployeeAccountService"/>（員工帳號管理）。
    /// 這支 Controller 現在只負責三件事：① 呼叫 Service、
    /// ② 依 <see cref="ServiceResult.Outcome"/> 決定要 View()/NotFound()/RedirectToAction()、
    /// ③ 把驗證錯誤塞回 ModelState。不應該再把任何資料庫查詢或業務規則直接寫回這裡，
    /// 新規則一律加進對應的 Service。
    /// </para>
    /// </summary>
    [EmployeeAuthorize(RoleNames.Admin)] // 僅限管理員存取
    public class SettingController : Controller
    {
        private readonly IRoleManagementService _roleService;
        private readonly IEmployeeAccountService _employeeService;
        private readonly ICurrentUserService _currentUser;

        public SettingController(
            IRoleManagementService roleService,
            IEmployeeAccountService employeeService,
            ICurrentUserService currentUser)
        {
            _roleService = roleService;
            _employeeService = employeeService;
            _currentUser = currentUser;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Roles));
        }

        #region 1. 角色權限管理 (Roles)

        public async Task<IActionResult> Roles()
        {
            var model = await _roleService.GetRolesOverviewAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var model = await _roleService.GetRoleForEditAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(RoleEditViewModel model)
        {
            // 標準 DataAnnotation 驗證（[Required] 角色名稱等）先擋，
            // 業務規則（重複命名、保留角色、權限合法性…）交給 Service 判斷
            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var result = await _roleService.UpdateRoleAsync(model, currentUserId);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = "角色權限已成功更新！";
                    return RedirectToAction(nameof(Roles));

                case OperationOutcome.NotFound:
                    return NotFound();

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                    return View(model);

                default: // Error
                    ModelState.AddModelError("", result.ErrorMessage ?? "更新角色權限時發生錯誤，請稍後再試。");
                    model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                    return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CreateRole()
        {
            var model = await _roleService.GetRoleForCreateAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var result = await _roleService.CreateRoleAsync(model, currentUserId);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = $"成功建立新角色：{model.RoleName.Trim()}";
                    return RedirectToAction(nameof(Roles));

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                    return View(model);

                default: // Error（CreateRole 不會有 NotFound 情境）
                    ModelState.AddModelError("", result.ErrorMessage ?? "新增角色失敗，請稍後再試。");
                    model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
                    return View(model);
            }
        }

        #endregion

        #region 2. 員工管理 (Employee List & Operations)

        // GET: Setting/UserList
        public async Task<IActionResult> UserList(string? keyword, int? roleId, string? status, int page = 1, int pageSize = 10)
        {
            var model = await _employeeService.GetUserListAsync(keyword, roleId, status, page, pageSize);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var model = await _employeeService.GetEmployeeForCreateAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var result = await _employeeService.CreateEmployeeAsync(model, currentUserId);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = $"成功建立員工帳號：{model.Name} ({model.Email})，員工編號：{model.EmployeeNo}";
                    return RedirectToAction(nameof(UserList));

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    // CreateEmployeeAsync 執行過程中可能已經重新產生過 model.EmployeeNo，
                    // 這裡不用自己額外處理，直接拿同一個 model 回填畫面即可
                    model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                    return View(model);

                default: // Error
                    ModelState.AddModelError("", result.ErrorMessage ?? "建立帳號過程發生錯誤，請稍後再試。");
                    model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                    return View(model);
            }
        }
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var model = await _employeeService.GetEmployeeForEditAsync(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "查無該員工資料。";
                return RedirectToAction(nameof(UserList));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                return View(model);
            }

            int currentUserId = GetCurrentUserId();
            var result = await _employeeService.UpdateEmployeeAsync(model, currentUserId);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    TempData["SuccessMessage"] = $"已成功修改員工資料：{model.Name}";
                    return RedirectToAction(nameof(UserList));

                case OperationOutcome.NotFound:
                    // [沿用原本行為] 這裡刻意不是生硬的 404——
                    // 代表「驗證都通過了，但真正寫入時卻發現這筆員工資料不見了」（例如被同時刪除的競態情況），
                    // 原本的使用者體驗是顯示錯誤訊息＋導回清單頁，這裡維持一致
                    TempData["ErrorMessage"] = "找不到該員工資料。";
                    return RedirectToAction(nameof(UserList));

                case OperationOutcome.ValidationFailed:
                    foreach (var (key, message) in result.Errors)
                    {
                        ModelState.AddModelError(key, message);
                    }
                    model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                    return View(model);

                default: // Error
                    ModelState.AddModelError("", result.ErrorMessage ?? "更新員工資料時發生錯誤，請稍後再試。");
                    model.AvailableRoles = await _employeeService.GetAvailableRolesAsync(excludeMemberRoles: true);
                    return View(model);
            }
        }

        // [已刪除] UpdateEmployeeStatus
        // 原本這支方法是給「列表頁快速切換員工狀態」用的獨立端點，
        // 經實測確認目前沒有任何 View 或前端呼叫它（註解掉後功能測試正常），
        // 屬於死代碼，這次重構順手移除。
        // 若未來要重新做「不經過完整編輯表單、直接切換狀態」的功能，
        // 可以參考 IEmployeeAccountService.UpdateEmployeeAsync 裡 Status 相關的驗證邏輯
        // （EmployeeStatuses.AllowedStatuses 白名單、自己不能停用自己）重新實作，
        // 對應的稽核動作常數 AuditActions.UpdateEmployeeStatus 也還保留著可以直接用。

        #endregion

        #region Private Helpers

        /// <summary>
        /// 取得目前登入者的 UserId。
        /// 實際解析邏輯委派給 <see cref="ICurrentUserService"/>，這裡只負責在
        /// 理論上不該發生、但真的發生時（Claims 解析不到）直接拋例外，
        /// 而不是悄悄 fallback 成 0──避免以 currentUserId 比對的保護邏輯（例如自我鎖定檢查）
        /// 被意外繞過，也避免寫入錯誤的 AuditLog.UserId = 0。
        /// </summary>
        private int GetCurrentUserId()
        {
            return _currentUser.UserId
                ?? throw new InvalidOperationException("無法取得目前登入使用者的識別碼。");
        }

        #endregion
    }
}