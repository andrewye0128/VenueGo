using System.Threading;
using System.Threading.Tasks;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.ViewModels.Shared;

namespace VenueGo.Services.Members
{
    /// <summary>
    /// 會員查詢服務。
    /// <para>
    /// 【為何不把查詢寫在 Controller】新增預約的步驟 1 要查會員清單，
    /// 會員管理頁也要查，期末的前台同樣要查。若查詢寫在 Controller，
    /// 「只顯示一般會員、排除停權」這條規則就會被複製好幾份，
    /// 其中一份忘了改就會出現停權會員仍可被預約的漏洞。
    /// </para>
    /// </summary>
    /// 

    // 解釋
    // PagedResult<MemberListItemViewModel> =>  回傳一頁的會員清單 +分頁資訊
    // MemberSearchCriteria =>  criteria 查詢條件（關鍵字、頁碼、是否含停權）
    // CancellationToken cancellationToken = default  =>  取消通知，有預設值所以可以不傳




    public interface IMemberQueryService
    {
        /// <summary>
        /// 依條件分頁查詢會員清單。只回傳具有一般會員角色的使用者。
        /// </summary>
        Task<PagedResult<MemberListItemViewModel>> SearchAsync(
            MemberSearchCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// 依 Id 取得單一會員。查無資料或該使用者不是一般會員時回傳 null。
        /// 供步驟 1 送出時做伺服器端驗證使用，不可只信任前端傳來的 Id。
        /// </summary>
        Task<MemberListItemViewModel?> GetSelectableMemberAsync(
            int userId, CancellationToken cancellationToken = default);
    }
}
