using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Constants;
using VenueGo.ViewModels.Shared;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 會員搜尋條件。由查詢字串繫結（例如 ?keyword=王&amp;page=2）。
    /// </summary>
    public class MemberSearchCriteria
    {
        /// <summary>預設每頁筆數。</summary>
        public const int DefaultPageSize = 5;

        /// <summary>每頁筆數上限，避免有人手改網址一次撈出整張表。</summary>
        public const int MaxPageSize = 50;

        /// <summary>關鍵字，同時比對姓名、手機與 Email。</summary>
        [Display(Name = "關鍵字")]
        [StringLength(100, ErrorMessage = "關鍵字請勿超過 100 個字。")]
        public string? Keyword { get; set; }

        /// <summary>
        /// 是否一併顯示停權或鎖定中的會員。
        /// 預設不顯示；但保留這個選項是為了讓管理員能分辨
        /// 「這位客人沒註冊」與「這位客人被停權」，
        /// 否則櫃檯會替同一個人重複建立帳號。
        /// </summary>
        [Display(Name = "顯示停權或鎖定中的會員")]
        public bool IncludeInactive { get; set; }

        /// <summary>頁碼，從 1 開始。</summary>
        public int Page { get; set; } = 1;

        /// <summary>每頁筆數。</summary>
        public int PageSize { get; set; } = DefaultPageSize;

        /// <summary>
        /// 將不合法的頁碼與筆數修正到合理範圍。
        /// 由 Service 在查詢前呼叫，Controller 不必自行檢查。
        /// </summary>
        public void Normalize()
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = DefaultPageSize;
            if (PageSize > MaxPageSize) PageSize = MaxPageSize;
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        }

        /// <summary>
        /// 轉成分頁連結需要的查詢條件，讓使用者翻頁時搜尋條件不會消失。
        /// </summary>
        public IDictionary<string, string> ToRouteValues()
        {
            var values = new Dictionary<string, string>();

            // 只加入真正有值的項目，避免網址出現 ?keyword=&includeInactive= 這種雜訊
            if (!string.IsNullOrWhiteSpace(Keyword)) values["keyword"] = Keyword;
            if (IncludeInactive) values["includeInactive"] = "true";
            if (PageSize != DefaultPageSize) values["pageSize"] = PageSize.ToString();

            return values;
        }
    }

    /// <summary>
    /// 會員清單的一列。
    /// </summary>
    public class MemberListItemViewModel
    {
        /// <summary>會員的 Users.UserId。</summary>
        public int UserId { get; init; }

        /// <summary>
        /// 會員編號。資料庫沒有這個欄位，由 UserId 格式化而來。
        /// 若日後在 Users 增加真正的 MemberNo 欄位，只需改這一處。
        /// </summary>
        public string MemberNo => $"M{UserId:D4}";

        /// <summary>姓名。</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>手機。</summary>
        public string Phone { get; init; } = string.Empty;

        /// <summary>Email。</summary>
        public string Email { get; init; } = string.Empty;

        /// <summary>Users.Status 的原始值。</summary>
        public string Status { get; init; } = UserStatuses.Active;

        /// <summary>帳號鎖定到期時間，null 表示未鎖定。</summary>
        public DateTime? LockedUntil { get; init; }

        /// <summary>手機條碼載具，供步驟 5 自動帶入發票載具。</summary>
        public string? CarrierNo { get; init; }

        /// <summary>
        /// 是否可被選為預約人。由 Service 判斷後寫入，
        /// 不在 ViewModel 內讀取系統時間，以免畫面與查詢的判斷基準不一致。
        /// </summary>
        public bool IsSelectable { get; init; } = true;

        /// <summary>
        /// 不可選的原因，顯示在清單上讓管理員知道為什麼不能選。
        /// 可選時為 null。
        /// </summary>
        public string? UnselectableReason { get; init; }

        /// <summary>狀態顯示文字。</summary>
        public string StatusText => Status switch
        {
            UserStatuses.Active => LockedUntil.HasValue ? "鎖定中" : "正常",
            UserStatuses.Suspended => "已停權",
            UserStatuses.Inactive => "已註銷",
            _ => Status
        };

        /// <summary>狀態徽章的 Bootstrap 樣式類別。</summary>
        public string StatusBadgeClass => IsSelectable
            ? "text-bg-success-subtle text-success-emphasis"
            : "text-bg-secondary-subtle text-secondary-emphasis";
    }

    /// <summary>
    /// 步驟 1「選擇會員」頁面的 ViewModel。
    /// </summary>
    public class SelectMemberViewModel
    {
        /// <summary>目前的搜尋條件，回填到搜尋框。</summary>
        public MemberSearchCriteria Criteria { get; set; } = new();

        /// <summary>查詢結果。</summary>
        public PagedResult<MemberListItemViewModel> Members { get; set; }
            = PagedResult<MemberListItemViewModel>.Empty();

        /// <summary>
        /// 目前已選的會員 Id。重新進入本步驟（例如從步驟 2 按上一步）時，
        /// 用它讓原本選的那一列保持勾選。
        /// </summary>
        public int? SelectedUserId { get; set; }

        /// <summary>分頁列的 ViewModel。</summary>
        public PagerViewModel Pager => PagerViewModel.FromPagedResult(
            Members, action: "SelectMember", controller: "ReservationCreate", routeValues: Criteria.ToRouteValues());
    }
}