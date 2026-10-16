using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>
    /// 評論專區的一張卡片。
    /// ⚠️ 沒有 ReviewId、沒有員工姓名、沒有顧客滿意度——結構上就不給（交接文件 4-1 第 1 點）。
    /// ⚠️ 屬性名稱就是 JSON 的欄位名稱，改名等於改規格，前端要跟著改。
    /// </summary>
    public sealed record PublicReviewCardDto(
        byte StarRating,
        string? Content,             // 公開版本：個資、連結、不雅字詞已遮蔽
        bool IsRatingOnly,           // 只給星等、沒有留言：卡片要換版型
        TimeView CreatedAt,
        bool MentionsVenue,
        bool MentionsStaff,
        string? VenueName,           // 場地名稱。10/15 起來自 v_ReviewPublicCard（INNER JOIN），實際上不會是 null
        string DisplayName,          // 匿名時是隨機暱稱，實名時是會員姓名
        PublicReplyDto? Reply,       // 沒有回覆是 null
        DateOnly? VisitedOn);        // 10/15：到館日期（第一次有效入場的那天），JSON 是 "2026-10-12"。只有日期，不給幾點
}
