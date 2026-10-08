using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 「我的個人資料」：目前登入者查看與修改自己的資料（姓名、電話、密碼）。
    /// <para>
    /// 【為何 userId 一律由呼叫端傳入、不讀 model.UserId】
    /// 原本 Profile(POST) 用表單隱藏欄位的 UserId 決定要改誰，
    /// 任何登入者只要改掉隱藏欄位，就能修改別人的姓名與電話。
    /// 現在 userId 只能來自 ICurrentUserService（驗證 Cookie 的 Claims，使用者改不了）。
    /// </para>
    /// </summary>
    public interface IUserProfileService
    {
        /// <summary>取得個人資料頁要顯示的資料；找不到回傳 null。</summary>
        Task<UserProfileViewModel?> GetProfileAsync(int userId);

        /// <summary>
        /// 更新姓名、電話，若有填新密碼則一併變更密碼。
        /// 驗證失敗時 Key 為 CurrentPassword（舊密碼沒填或錯誤）。
        /// </summary>
        Task<ServiceResult> UpdateProfileAsync(int userId, UserProfileViewModel model);
    }
}