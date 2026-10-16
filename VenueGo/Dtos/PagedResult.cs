namespace VenueGo.Dtos
{
    /// <summary>
    /// 分頁查詢的回傳格式：這一頁的資料 + 總筆數。
    /// 送到前端後屬性名會變成小寫開頭：items / totalCount / page / pageSize。
    /// <para>
    /// 【為何放在 Dtos 底下，不放 ReservationDtos】
    /// 分頁不是預約專屬的概念，之後「訂單與付款」等列表也能共用：
    /// PagedResult&lt;MyReservationDto&gt;、PagedResult&lt;MyOrderDto&gt;……
    /// </para>
    /// <para>
    /// 【為何回傳總筆數，不回傳總頁數】
    /// 前端分頁元件（UPagination）要的是總筆數，它會自己算頁數；
    /// 而且總筆數能算出總頁數，總頁數卻算不回總筆數。
    /// </para>
    /// </summary>
    /// <typeparam name="T">每一筆資料的型別，例如 MyReservationDto</typeparam>
    public class PagedResult<T>
    {
        /// <summary>這一頁的資料（最多 PageSize 筆）。</summary>
        public IReadOnlyList<T> Items { get; init; } = [];

        /// <summary>符合條件的「總」筆數，不是這一頁的筆數。</summary>
        public int TotalCount { get; init; }

        /// <summary>第幾頁（從 1 開始）。</summary>
        public int Page { get; init; }

        /// <summary>每頁幾筆。</summary>
        public int PageSize { get; init; }
    }
}
