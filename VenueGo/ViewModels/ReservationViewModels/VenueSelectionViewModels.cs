using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using VenueGo.ViewModels.Shared;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 場地搜尋條件。由查詢字串繫結（例如 ?sportTypeId=1&amp;keyword=A&amp;page=2）。
    /// </summary>
    public class VenueSearchCriteria
    {
        /// <summary>預設每頁筆數。卡片為兩欄版面，取 6 可排成三列。</summary>
        public const int DefaultPageSize = 6;

        /// <summary>每頁筆數上限，避免有人手改網址一次撈出整張表。</summary>
        public const int MaxPageSize = 30;

        /// <summary>運動類型。null 表示不限。</summary>
        [Display(Name = "運動類型")]
        public int? SportTypeId { get; set; }

        /// <summary>關鍵字，比對場地名稱。</summary>
        [Display(Name = "場地名稱")]
        [StringLength(50, ErrorMessage = "關鍵字請勿超過 50 個字。")]
        public string? Keyword { get; set; }

        /// <summary>頁碼，從 1 開始。</summary>
        public int Page { get; set; } = 1;

        /// <summary>每頁筆數。</summary>
        public int PageSize { get; set; } = DefaultPageSize;

        /// <summary>將不合法的頁碼與筆數修正到合理範圍。</summary>
        public void Normalize()
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = DefaultPageSize;
            if (PageSize > MaxPageSize) PageSize = MaxPageSize;
            if (SportTypeId is <= 0) SportTypeId = null;
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        }

        /// <summary>轉成分頁連結需要的查詢條件，讓使用者翻頁時篩選條件不會消失。</summary>
        public IDictionary<string, string> ToRouteValues()
        {
            var values = new Dictionary<string, string>();

            if (SportTypeId.HasValue) values["sportTypeId"] = SportTypeId.Value.ToString();
            if (!string.IsNullOrWhiteSpace(Keyword)) values["keyword"] = Keyword;
            if (PageSize != DefaultPageSize) values["pageSize"] = PageSize.ToString();

            return values;
        }
    }

    /// <summary>
    /// 場地卡片的資料。
    /// </summary>
    public class VenueCardViewModel
    {
        /// <summary>Venues.VenueId。</summary>
        public int VenueId { get; init; }

        /// <summary>場地名稱。</summary>
        public string VenueName { get; init; } = string.Empty;

        /// <summary>運動類型名稱，取自 SportTypes.SportName。</summary>
        public string SportName { get; init; } = string.Empty;

        /// <summary>可容納人數。資料庫允許為 null，代表尚未設定。</summary>
        public int? Capacity { get; init; }

        /// <summary>場地照片路徑。null 時畫面顯示替代圖示。</summary>
        public string? PhotoPath { get; init; }

        /// <summary>場地是否啟用。</summary>
        public bool IsActive { get; init; }

        /// <summary>離峰單價（每小時）。查無計價規則時為 null。</summary>
        public int? OffPeakPrice { get; init; }

        /// <summary>尖峰單價（每小時）。查無計價規則時為 null。</summary>
        public int? PeakPrice { get; init; }

        /// <summary>尖峰起始時間。null 表示全天同價。</summary>
        public TimeOnly? PeakStartTime { get; init; }

        /// <summary>是否可被選為預約場地。</summary>
        public bool IsSelectable => IsActive;

        /// <summary>狀態顯示文字。</summary>
        public string StatusText => IsActive ? "可預約" : "維護中";

        /// <summary>
        /// 狀態文字的 Bootstrap 樣式類別。
        /// 設計稿的狀態是規格列中的一行綠字，而非右上角的徽章，
        /// 因此這裡只需要文字顏色。
        /// </summary>
        public string StatusTextClass => IsActive ? "text-success" : "text-secondary";

        /// <summary>不可選的原因，供畫面提示管理員。</summary>
        public string? UnselectableReason => IsActive
            ? null
            : "此場地目前停用，無法建立預約。";

        /// <summary>可容納人數顯示文字。未設定時明確標示，避免顯示空白讓人誤以為是 0。</summary>
        public string CapacityText => Capacity.HasValue ? $"{Capacity} 人" : "未設定";

        /// <summary>
        /// 「參考價格」規格列要顯示的資料。尖峰/離峰同價（或未設定）時只有一列，
        /// 不同價時有兩列。View 只需要把這份清單畫出來，不用自己判斷要顯示幾列。
        /// </summary>
        public IReadOnlyList<PriceRow> PriceRows
        {
            get
            {
                if (OffPeakPrice is null)
                {
                    return new[] { new PriceRow { Label = "參考價格", Value = "未設定" } };
                }

                var hasDifferentPeakPrice =
                    PeakPrice.HasValue && PeakStartTime.HasValue && PeakPrice != OffPeakPrice;

                if (!hasDifferentPeakPrice)
                {
                    return new[]
                    {
                        new PriceRow { Label = "參考價格", Value = $"NT$ {OffPeakPrice:N0} / 小時" }
                    };
                }

                return new[]
                {
                    new PriceRow { Label = "離峰價格", Value = $"NT$ {OffPeakPrice:N0} / 小時" },
                    new PriceRow { Label = "尖峰價格", Value = $"NT$ {PeakPrice:N0} / 小時（{PeakStartTime:HH\\:mm} 起）" }
                };
            }
        }
    }

    /// <summary>
    /// 場地卡片「參考價格」規格列的一列資料。
    /// </summary>
    public class PriceRow
    {
        public string Label { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }

    /// <summary>
    /// 步驟 2「選擇場地」頁面的 ViewModel。
    /// </summary>
    public class SelectVenueViewModel
    {
        /// <summary>目前的搜尋條件，回填到篩選列。</summary>
        public VenueSearchCriteria Criteria { get; set; } = new();

        /// <summary>查詢結果。</summary>
        public PagedResult<VenueCardViewModel> Venues { get; set; }
            = PagedResult<VenueCardViewModel>.Empty();

        /// <summary>運動類型下拉選單的選項。</summary>
        public IReadOnlyList<SportTypeOption> SportTypes { get; set; }
            = Array.Empty<SportTypeOption>();

        /// <summary>
        /// 目前已選的場地 Id。從步驟 3 按上一步回來時，
        /// 用它讓原本選的那張卡片保持選取狀態。
        /// </summary>
        public int? SelectedVenueId { get; set; }

        /// <summary>步驟 1 已選的會員姓名，顯示在頁面上方的資訊條。</summary>
        public string? MemberName { get; set; }

        /// <summary>步驟 1 已選的會員手機，顯示在頁面上方的資訊條。</summary>
        public string? MemberPhone { get; set; }

        /// <summary>分頁列的 ViewModel。</summary>
        public PagerViewModel Pager => PagerViewModel.FromPagedResult(
            Venues, action: "SelectVenue", controller: "ReservationCreate", routeValues: Criteria.ToRouteValues());
    }

    /// <summary>
    /// 運動類型下拉選單的一個選項。
    /// </summary>
    public class SportTypeOption
    {
        public int SportTypeId { get; init; }
        public string SportName { get; init; } = string.Empty;
    }
}