using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 員工帳號管理的商業邏輯，從 SettingController 搬出來。
    /// <para>
    /// 涵蓋員工清單查詢、建立員工（含員工編號產生與併發重試）、
    /// 編輯員工資料（含角色異動、離職快照記錄）。
    /// </para>
    /// </summary>
    public interface IEmployeeAccountService
    {
        /// <summary>員工清單頁（UserList），含關鍵字/角色/狀態篩選跟分頁。</summary>
        Task<UserListViewModel> GetUserListAsync(string? keyword, int? roleId, string? status, int page, int pageSize);

        /// <summary>取得「新增員工」畫面要的初始資料（可選角色清單、自動產生的員工編號）。</summary>
        Task<RegisterUserViewModel> GetEmployeeForCreateAsync();

        /// <summary>
        /// 建立員工帳號。
        /// <para>
        /// 【注意】這個方法會直接修改傳入的 <paramref name="model"/>.EmployeeNo——
        /// 不論成功或失敗，呼叫完之後 model.EmployeeNo 都會是「目前實際使用/建議使用」的編號，
        /// 失敗時 Controller 可以直接把同一個 model 拿去 View()，畫面上顯示的編號會是最新的，
        /// 不用自己額外追蹤。
        /// </para>
        /// </summary>
        Task<ServiceResult> CreateEmployeeAsync(RegisterUserViewModel model, int currentUserId);

        /// <summary>
        /// 取得單一員工的編輯用資料；找不到回傳 null。
        /// 如果該員工目前是離職/留停狀態，會順便查出上次異動時被移除的角色快照（PreviousRoleNames）。
        /// </summary>
        Task<EditUserViewModel?> GetEmployeeForEditAsync(int userId);

        /// <summary>
        /// 更新員工資料（基本資料、在職狀態、角色指派）。
        /// <para>
        /// 【NotFound 的特殊處理】這裡的 NotFound 代表「驗證都通過了，
        /// 但真正要寫入時卻發現這筆員工資料不見了」（例如被其他操作同時刪除的競態情況），
        /// 跟一般「網址打錯」的 404 情境不同，Controller 端建議對應成
        /// TempData 錯誤訊息 + RedirectToAction(UserList)，而不是直接回 404 頁面，
        /// 這是沿用原本 SettingController 的使用者體驗設計。
        /// </para>
        /// </summary>
        Task<ServiceResult> UpdateEmployeeAsync(EditUserViewModel model, int currentUserId);

        /// <summary>目前啟用中的角色清單，供新增/編輯員工畫面的角色勾選使用。</summary>
        Task<List<RoleOptionDto>> GetAvailableRolesAsync(bool excludeMemberRoles = false);
    }
}