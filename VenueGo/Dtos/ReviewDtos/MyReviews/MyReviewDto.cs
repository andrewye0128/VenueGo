using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.MyReviews
{

    /// <summary>
    /// 我的評論（規格 2-4），員工預覽（2-8）也用同一個。
    /// ⚠️ 沒有 ReviewId。前端用網址上的憑證識別，不需要它。
    /// ⚠️ 屬性名稱就是 JSON 的欄位名稱，改名等於改規格，前端要跟著改。
    /// </summary>
    public sealed record MyReviewDto(
        string Kind,
        ReviewContextDto Context,
        string DisplayName,
        bool IsAnonymous,
        byte StarRating,
        string? Content,             // 9/28 起是「公開版本」：個資、連結、不雅字詞已遮蔽，跟其他顧客看到的一樣
        bool ContentMasked,          // 有沒有東西被遮蔽：有的話畫面加一行說明，作者才不會以為系統吃掉了字
        bool IsRatingOnly,
        bool MentionsVenue,
        bool MentionsStaff,
        TimeView CreatedAt,
        bool IsPublic,
        // 10/27：顧客選公開的話，從什麼時候起會出現在評論專區：館方回覆的時間，或建立後滿 ReviewPolicy.PublicBufferDays 天，取比較早的
        TimeView PublicFrom,
        // 10/27：「現在」是不是已經過了 PublicFrom（不管顧客有沒有選公開）。公開開關旁顯示「尚未公開／已公開」用
        bool PublicFromReached,
        bool IsSpamMarked,
        string? SpamReasonText,      // 被下架時給顧客看的理由（ReviewPolicy.SpamReasonInfos 的 CustomerText）；沒下架是 null
        bool CanToggleVisibility,
        MyReplyDto? Reply);
}
