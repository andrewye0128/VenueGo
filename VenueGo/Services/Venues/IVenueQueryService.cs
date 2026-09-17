using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.ViewModels.Shared;

namespace VenueGo.Services.Venues
{
    /// <summary>
    /// 場地查詢服務。
    /// <para>
    /// 與 IMemberQueryService 相同的理由：新增預約的步驟 2 要查場地，
    /// 場地管理頁、期末前台的場地瀏覽也都要查。
    /// 把「是否可預約」的判斷集中在一處，才不會有某個頁面漏了 IsActive 的檢查，
    /// 讓維護中的場地被預約出去。
    /// </para>
    /// </summary>
    public interface IVenueQueryService
    {
        /// <summary>依條件分頁查詢場地清單。含停用的場地，由呼叫端依 IsSelectable 決定是否可選。</summary>
        Task<PagedResult<VenueCardViewModel>> SearchAsync(
            VenueSearchCriteria criteria, CancellationToken cancellationToken = default);

        /// <summary>
        /// 依 Id 取得可預約的場地。查無資料或場地已停用時回傳 null。
        /// 供步驟 2 送出時做伺服器端驗證，不可只信任前端傳來的 Id。
        /// </summary>
        Task<VenueCardViewModel?> GetSelectableVenueAsync(
            int venueId, CancellationToken cancellationToken = default);

        /// <summary>取得啟用中的運動類型，供下拉選單使用。</summary>
        Task<IReadOnlyList<SportTypeOption>> GetSportTypeOptionsAsync(
            CancellationToken cancellationToken = default);
    }
}