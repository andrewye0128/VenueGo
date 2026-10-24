namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    // ════════════════════════════════════════════════════════════════
    //  評論專區 API（規格 2-1）的回應
    //
    //  ── 這個資料夾的檔案（10/10 從 ReviewDtos_Claude.cs 拆出來）─────────
    //  一個型別一個檔案，檔名＝型別名稱；命名空間跟資料夾一致。
    //  為什麼另外寫 DTO、不直接回傳 Entity 或 ViewModel：
    //  只放前端需要的欄位，從結構上保證不會多送（例如 ReviewId）。
    //
    //  ⚠️ 屬性名稱就是 JSON 的欄位名稱（自動轉成小寫開頭）。改名等於改規格，前端要跟著改。
    //  ⚠️ 組裡規定：data 裡不要用 message、success、errorCode 當欄位名稱（會跟外層 ApiResult 混淆）。
    // ════════════════════════════════════════════════════════════════

    /// <summary>評論專區的完整回應：篩選條件、選項、分頁、統計、卡片。</summary>
    public sealed record PublicReviewListDto(
        ReviewFilterDto Filter,
        ReviewDefaultsDto Defaults,
        ReviewOptionsDto Options,
        IReadOnlyList<SportTabDto> SportTabs,
        RatingSummaryDto Summary,
        IReadOnlyList<PublicReviewCardDto> Items,
        int Page,
        int TotalPages,
        // 10/23 分頁：每一頁第一則的星等與日期，第 n 頁是 PageMarks[n - 1]。
        // 長按快轉換頁時，前台用它顯示「正在經過哪裡」，不必一頁一頁去要資料。首頁 Slide（有給 take）時是空的。
        IReadOnlyList<PageMarkDto> PageMarks,
        // 10/17：沒選星等時，只給星等的評論不列在 Items 裡，這裡是沒列出的則數（前台顯示「另有 N 則」）。
        // 選了星等、或符合條件的評論一則有文字的都沒有時，全部都會列出，這裡是 0。
        int RatingOnlyHidden);
}
