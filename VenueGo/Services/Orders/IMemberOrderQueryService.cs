using VenueGo.Dtos;
using VenueGo.Dtos.OrderDtos;

namespace VenueGo.Services.Orders
{
    /// <summary>
    /// 前台會員「訂單與付款」的查詢。
    /// <para>
    /// 只回傳這位會員、有預約、預約沒有作廢的訂單（跟「我的預約」同一個範圍）。
    /// </para>
    /// </summary>
    public interface IMemberOrderQueryService
    {
        /// <summary>
        /// 我的訂單列表（篩選、分頁）。排序：下單時間新到舊 → 訂單 Id 大到小。
        /// 查無資料時 Items 是空的，不是 null。
        /// </summary>
        Task<PagedResult<MyOrderDto>> GetMyOrdersAsync(
            int userId, MyOrderQuery query, CancellationToken cancellationToken = default);

        /// <summary>
        /// 一筆訂單的明細。不存在、不是這位會員的、預約已作廢，一律回傳 null（Controller 回 404）。
        /// </summary>
        Task<MyOrderDetailDto?> GetMyOrderAsync(
            int userId, int orderId, CancellationToken cancellationToken = default);
    }
}