using System.Threading;
using System.Threading.Tasks;
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
    /// 【誰會用到】預約詳細頁、訂單管理的詳細頁。
    /// 兩者顯示的資料幾乎相同，共用同一個查詢即可。
    /// </para>
    /// </summary>
    public interface IReservationQueryService
    {
        /// <summary>
        /// 取得預約詳細頁所需的完整資料。查無此預約時回傳 null。
        /// </summary>
        Task<ReservationDetailViewModel?> GetDetailAsync(
            int reservationId, CancellationToken cancellationToken = default);
    }
}