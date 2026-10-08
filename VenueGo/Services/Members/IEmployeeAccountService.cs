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


        // ============================================================
        // 會員升格為員工（原本寫在 MemberController，並且重複了員工編號產生與角色查詢）
        // ============================================================

        /// <summary>
        /// 取得「會員升格為員工」畫面的初始資料（含自動產生的員工編號、可選角色）。
        /// NotFound = 查無此使用者；ValidationFailed = 該使用者已經是員工（Errors 內有訊息）。
        /// </summary>
        Task<ServiceResult<ConvertEmployeeViewModel>> GetConvertToEmployeeFormAsync(int userId);

        /// <summary>
        /// 「會員升格為員工」可選的角色：啟用中，且排除 Member 與舊的 Customer 角色。
        /// 驗證失敗要重新顯示畫面時，用這個方法補回角色清單。
        /// </summary>
        Task<List<RoleOptionDto>> GetConvertibleRolesAsync();

        /// <summary>
        /// 把既有會員升格為員工。成功時 Data 為會員姓名（取自資料庫，不是表單送來的值）。
        /// <para>
        /// 驗證規則：使用者必須存在且還不是員工；員工編號不可空白、不可重複；
        /// 至少選一個「啟用中、可指派」的後台角色（否則會變成無法登入後台的帳號，
        /// 跟 <see cref="CreateEmployeeAsync"/> 的規則一致）。
        /// </para>
        /// <para>
        /// 【為何姓名、Email 不用表單的值】ConvertToEmployee 畫面用隱藏欄位送 Name / Email，
        /// 任何人都能改。稽核紀錄與成功訊息一律用從資料庫查到的值。
        /// </para>
        /// </summary>
        Task<ServiceResult<string>> ConvertMemberToEmployeeAsync(ConvertEmployeeViewModel model, int currentUserId);
    }
}