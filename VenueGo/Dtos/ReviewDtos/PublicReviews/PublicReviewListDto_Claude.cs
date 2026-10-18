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
        int TotalPages);
}
