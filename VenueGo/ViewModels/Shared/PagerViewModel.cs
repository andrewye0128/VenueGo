using System;
using System.Collections.Generic;

namespace VenueGo.ViewModels.Shared
{
    /// <summary>
    /// 分頁列（_Pager.cshtml）的 ViewModel。
    /// <para>
    /// 【為何與 <see cref="PagedResult{T}"/> 分開】PagedResult 關心的是資料，
    /// 本類別關心的是「分頁連結要連到哪個 Action、要帶哪些查詢條件」。
    /// 分開之後，同一個分頁列元件可以被任何列表頁重複使用，
    /// 不必為了配合元件而去改資料層的型別。
    /// </para>
    /// </summary>
    public class PagerViewModel
    {
        /// <summary>目前頁碼。</summary>
        public int Page { get; init; } = 1;

        /// <summary>總頁數。</summary>
        public int TotalPages { get; init; } = 1;

        /// <summary>分頁連結要連到的 Action 名稱。</summary>
        public string Action { get; init; } = string.Empty;

        /// <summary>分頁連結要連到的 Controller 名稱。留 null 表示目前的 Controller。</summary>
        public string? Controller { get; init; }

        /// <summary>
        /// 除頁碼以外要一併帶上的查詢條件，例如關鍵字、是否顯示停權會員。
        /// 若不帶，使用者翻到第 2 頁時搜尋條件就會消失。
        /// </summary>
        public IDictionary<string, string> RouteValues { get; init; }
            = new Dictionary<string, string>();

        /// <summary>最多顯示幾個頁碼按鈕，頁數過多時以目前頁為中心顯示。</summary>
        public int MaxPageLinks { get; init; } = 5;

        /// <summary>要顯示的第一個頁碼。</summary>
        public int FirstVisiblePage
        {
            get
            {
                var half = MaxPageLinks / 2;
                var start = Page - half;
                if (start < 1) start = 1;
                if (start + MaxPageLinks - 1 > TotalPages)
                {
                    start = TotalPages - MaxPageLinks + 1;
                }
                return start < 1 ? 1 : start;
            }
        }

        /// <summary>要顯示的最後一個頁碼。</summary>
        public int LastVisiblePage =>
            Math.Min(FirstVisiblePage + MaxPageLinks - 1, TotalPages);

        /// <summary>頁數只有一頁時不需要顯示分頁列。</summary>
        public bool ShouldRender => TotalPages > 1;

        /// <summary>
        /// 產生指定頁碼的完整 route values（查詢條件 + 頁碼），供 asp-all-route-data 使用。
        /// </summary>
        public IDictionary<string, string> RouteValuesForPage(int page)
        {
            var values = new Dictionary<string, string>(RouteValues)
            {
                ["page"] = page.ToString()
            };
            return values;
        }

        /// <summary>
        /// 由分頁結果建立分頁列 ViewModel，避免在每個 View 裡重複組裝。
        /// </summary>
        public static PagerViewModel FromPagedResult<T>(
            PagedResult<T> result,
            string action,
            IDictionary<string, string>? routeValues = null,
            string? controller = null) => new()
            {
                Page = result.Page,
                TotalPages = result.TotalPages,
                Action = action,
                Controller = controller,
                RouteValues = routeValues ?? new Dictionary<string, string>()
            };
    }
}