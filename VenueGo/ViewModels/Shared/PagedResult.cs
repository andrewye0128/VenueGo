using System;
using System.Collections.Generic;

namespace VenueGo.ViewModels.Shared
{
    /// <summary>
    /// 泛型分頁結果。
    /// <para>
    /// 【為何抽成泛型】專案中至少有四處需要分頁：新增預約的會員清單與場地清單、
    /// 預約列表、訂單列表、會員管理列表。若每個頁面各自計算總頁數與筆數區間，
    /// 很容易出現「共 28 筆卻顯示 6 頁」這類算錯的情況。
    /// 把計算集中在這裡，各頁面只負責查資料。
    /// </para>
    /// </summary>
    /// <typeparam name="T">清單項目的型別，通常是某個 ViewModel。</typeparam>
    public class PagedResult<T>
    {
        /// <summary>本頁的資料列。</summary>
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        /// <summary>目前頁碼，從 1 開始。</summary>
        public int Page { get; init; } = 1;

        /// <summary>每頁筆數。</summary>
        public int PageSize { get; init; } = 10;

        /// <summary>符合條件的總筆數（不是本頁筆數）。</summary>
        public int TotalCount { get; init; }

        /// <summary>總頁數。無資料時回傳 1，避免畫面顯示「第 1 / 0 頁」。</summary>
        public int TotalPages =>
            TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        /// <summary>是否有上一頁。</summary>
        public bool HasPreviousPage => Page > 1;

        /// <summary>是否有下一頁。</summary>
        public bool HasNextPage => Page < TotalPages;

        /// <summary>本頁第一筆的序號，供「顯示 1 - 5 / 共 28 筆」使用。無資料時為 0。</summary>
        public int FirstItemNumber => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;

        /// <summary>本頁最後一筆的序號，供「顯示 1 - 5 / 共 28 筆」使用。</summary>
        public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);

        /// <summary>是否完全沒有符合條件的資料，供畫面顯示空狀態訊息。</summary>
        public bool IsEmpty => TotalCount == 0;

        /// <summary>
        /// 建立一個空的分頁結果，供查無資料或參數不合法時回傳，
        /// 避免在畫面上處理 null。
        /// </summary>
        public static PagedResult<T> Empty(int page = 1, int pageSize = 10) => new()
        {
            Items = Array.Empty<T>(),
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        };
    }
}