using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 角色與權限管理的商業邏輯，從 SettingController 搬出來。
    /// <para>
    /// 【為何要搬】原本角色名稱重複檢查、保留角色名稱保護、「自己把自己鎖在外面」的防呆，
    /// 全部混在 Controller 的 Action 裡，一支 EditRole(POST) 就有上百行。
    /// Controller 現在只負責：呼叫這裡 → 依 <see cref="ServiceResult.Outcome"/> 決定要
    /// View()、NotFound() 還是 RedirectToAction()，驗證規則本身跟畫面無關，能獨立測試。
    /// </para>
    /// </summary>
    public interface IRoleManagementService
    {
        /// <summary>角色清單頁（Roles.cshtml）要的完整資料：每個角色的人數、權限名稱。</summary>
        Task<List<RoleListItemViewModel>> GetRolesOverviewAsync();

        /// <summary>取得單一角色的編輯用資料；找不到回傳 null（Controller 對應回 404）。</summary>
        Task<RoleEditViewModel?> GetRoleForEditAsync(int roleId);

        /// <summary>取得「新增角色」畫面要的初始資料（目前只需要可選權限清單）。</summary>
        Task<RoleCreateViewModel> GetRoleForCreateAsync();

        /// <summary>目前啟用中的權限清單，供新增/編輯角色畫面的 checkbox 使用。</summary>
        Task<List<PermissionOptionDto>> GetAvailablePermissionsAsync();

        /// <summary>
        /// 更新角色（名稱、描述、啟用狀態、權限指派）。
        /// 內含的驗證規則：保留角色（Admin/Member）不可改名、不可跟其他角色重名、
        /// 已有使用者使用的角色不可改名、Admin 角色不可停用、不可把自己的管理員權限全部移除、
        /// 選擇的權限必須都是真實存在且啟用中的權限。
        /// </summary>
        Task<ServiceResult> UpdateRoleAsync(RoleEditViewModel model, int currentUserId);

        /// <summary>
        /// 建立新角色。內含的驗證規則：不可跟既有角色重名、不可使用保留角色名稱、
        /// 選擇的權限必須都是真實存在且啟用中的權限。
        /// </summary>
        Task<ServiceResult> CreateRoleAsync(RoleCreateViewModel model, int currentUserId);
    }
}