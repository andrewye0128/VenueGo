using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 後台「會員帳號管理」的商業邏輯，從 MemberController 搬出來。
    /// <para>
    /// 【跟 <see cref="IMemberQueryService"/> 的差別】IMemberQueryService 是給「新增預約」用的，
    /// 只回傳可被預約的會員（排除停權）、回傳預約模組自己的 ViewModel。
    /// 這裡是給管理員看的：要看得到停權／註銷的會員、要能停權、重設密碼、解鎖、匯出，
    /// 兩者的規則不同，刻意不合併，免得改其中一邊影響另一邊。
    /// </para>
    /// </summary>
    public interface IMemberAccountService
    {
        /// <summary>
        /// 重設密碼時使用的預設密碼。
        /// 會員管理畫面的確認視窗文字也寫死了這個值，改的時候記得兩邊一起改。
        /// </summary>
        const string DefaultResetPassword = "Pass@1234";

        /// <summary>
        /// 會員清單（擁有 Member 角色的所有使用者，含員工），支援關鍵字、狀態篩選與分頁。
        /// pageSize 會被限制在 1～100，page 最小為 1。
        /// </summary>
        Task<UserListViewModel> GetMemberListAsync(string? keyword, string? status, int page, int pageSize);

        /// <summary>取得單一使用者的詳細資料（Modal 用）；找不到回傳 null。</summary>
        Task<MemberDetailViewModel?> GetMemberDetailAsync(int userId);

        /// <summary>
        /// 變更會員狀態（Active / Suspended / Inactive）。成功時 Data 為會員姓名。
        /// 驗證規則：狀態必須是合法值；不可把「自己」的帳號改成非 Active（以免被鎖在系統外）。
        /// </summary>
        Task<ServiceResult<string>> UpdateMemberStatusAsync(int userId, string status, int currentUserId);

        /// <summary>
        /// 把密碼重設為 <see cref="DefaultResetPassword"/>，並清除登入失敗次數與鎖定。
        /// 成功時 Data 為會員姓名。
        /// </summary>
        Task<ServiceResult<string>> ResetPasswordAsync(int userId, int currentUserId);

        /// <summary>解除帳號鎖定（清除登入失敗次數與鎖定時間）。成功時 Data 為會員姓名。</summary>
        Task<ServiceResult<string>> UnlockAccountAsync(int userId, int currentUserId);

        /// <summary>
        /// 匯出所有會員的 CSV（UTF-8 含 BOM，Excel 直接開啟不會亂碼）。
        /// 欄位內的雙引號會正確跳脫，並避免 =、+、-、@ 開頭的內容被 Excel 當成公式執行。
        /// </summary>
        Task<byte[]> ExportMembersCsvAsync();
    }
}