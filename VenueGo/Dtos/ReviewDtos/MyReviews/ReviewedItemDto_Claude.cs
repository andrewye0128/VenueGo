using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.MyReviews
{
    /// <summary>
    /// 「我的評論 › 已評論」收合時的一列（10/10）。展開後的完整內容用既有的「查看我的評論」API（規格 2-4）。
    /// 為什麼不直接回傳完整的 MyReviewDto：清單只需要摘要；完整內容展開時才讀，
    /// 「顧客已看過回覆」也才會在真的看到回覆時記錄，不會因為打開清單就被當成已讀。
    /// ⚠️ 沒有 ReviewId。⚠️ 屬性名稱就是 JSON 的欄位名稱。
    /// </summary>
    public sealed record ReviewedItemDto(
        string Kind,                 // ReviewKind 的值：visit／booking
        int Ticket,                  // 現場評論：憑證 Id；預約評論：訂單 Id
        ReviewContextDto Context,
        byte StarRating,
        string? Excerpt,             // 內容開頭（已遮蔽個資、不雅字詞），只給星等時是 null
        TimeView CreatedAt,
        bool IsPublic,
        bool IsSpamMarked,
        bool HasReply,
        bool HasUnviewedReply);      // 館方回覆了、但顧客還沒看過：清單上顯示「新回覆」
}
