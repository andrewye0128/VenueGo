using System.Threading;
using System.Threading.Tasks;
using VenueGo.Dtos;
using VenueGo.Dtos.ReservationDtos;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約查詢服務：負責讀取資料並組成畫面所需的內容。
    /// <para>
    /// 【為何與異動服務分開】查詢不需要交易、也不會改資料；
    /// 異動需要交易、要寫稽核紀錄。兩者的關注點完全不同，
    /// 混在一個類別裡會讓建構式塞滿只有一半方法用得到的依賴。
    /// </para>
    /// <para>
    /// 【誰會用到】
    /// 後台：預約詳細頁、訂單管理的詳細頁（GetDetailAsync）。
    /// 前台：會員中心「我的預約」列表（GetMyReservationsAsync）。
    /// </para>
    /// </summary>
    public interface IReservationQueryService
    {
        /// <summary>
        /// 取得預約詳細頁所需的完整資料。查無此預約時回傳 null。
        /// </summary>
        Task<ReservationDetailViewModel?> GetDetailAsync(
            int reservationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得某位會員的預約列表（篩選、排序、分頁）。
        /// <para>
        /// 只回傳這位會員的預約，不含已作廢。
        /// 排序：建立時間新到舊 → 預約編號大到小。
        /// </para>
        /// <para>
        /// 查無資料時回傳空的 Items（TotalCount = 0），不是 null。
        /// </para>
        /// </summary>
        /// <param name="userId">會員的 UserId（由 Controller 決定是誰）</param>
        /// <param name="query">篩選條件與分頁</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢</param>
        Task<PagedResult<MyReservationDto>> GetMyReservationsAsync(
            int userId,
            MyReservationQuery query,
            CancellationToken cancellationToken = default);
    }
}