using VenueGo.Helpers;

namespace VenueGo.Dtos.Reviews
{
    // ════════════════════════════════════════════════════════════════
    //  顧客端評論 API 的回應形狀（對應《API規格_CReview.md》第二節）
    //
    //  ── 為什麼另外寫一組，不直接回傳 ViewModel ─────────────────────
    //  ViewModel 是給 Razor View 用的，裡面有 View 才需要的東西
    //  （[DisplayName]、ReviewId、QrToken⋯），直接序列化會把它們全部送出去。
    //  其中 ReviewId 是明確不能外流的（交接文件 4-1 第 1 點）。
    //  另外寫一組「只放前端需要的欄位」的類別，是從結構上保證不會多送。
    //
    //  ── 為什麼用 record ──────────────────────────────────────────
    //  這些類別只是「裝資料送出去」，建立之後不會再改。
    //  record 一行就寫完一個有唯讀屬性的類別，而且序列化成 JSON 時
    //  屬性名會自動轉成小寫開頭（StarRating → starRating），跟規格一致。
    //
    //  ⚠️ 屬性名稱就是 JSON 的欄位名稱。改名等於改規格，前端要跟著改。
    //  ⚠️ 組裡規定：data 裡不要用 message、success、errorCode 當欄位名稱（會跟外層 ApiResult 混淆）。
    //
    //  9/29：從 Api/Models 搬到 Dtos/Reviews（組員開發注意事項：給前台的資料格式放 Dtos/）。
    //        從資料表／ViewModel 轉成這些格式的程式在 Mappers/ReviewMapper。
    // ════════════════════════════════════════════════════════════════

    /// <summary>評論種類，也是網址的一段（/api/reviews/visit/...、/api/reviews/booking/...）。</summary>
    public static class ReviewKind
    {
        public const string Visit = "visit";       // 現場評論，憑 QRToken
        public const string Booking = "booking";   // 預約評論，憑 ReviewPerBookingId，要會員本人登入
    }

    /// <summary>
    /// 畫面上要顯示的時間（規格 0-4）。
    /// Ago／Full 在後端用 TimeAgo 算好：規則只存在一個地方，而且「現在」是校時過的時間。
    /// </summary>
    public sealed record TimeView(DateTime Value, string Ago, string Full)
    {
        public static TimeView From(DateTime time) => new(time, TimeAgo.Of(time), TimeAgo.Full(time));
    }

    /// <summary>下拉選單的一個選項。</summary>
    public sealed record OptionItem(string Value, string Text);

    // ── 2-1 評論專區 ─────────────────────────────────────────────

    public sealed record ReviewFilterDto(int? SportTypeId, string Range, int? Star, bool HasContentOnly, string Sort);

    public sealed record ReviewDefaultsDto(string Range, string Sort);

    public sealed record ReviewOptionsDto(IReadOnlyList<OptionItem> Ranges, IReadOnlyList<OptionItem> Sorts);

    public sealed record StarBarDto(int Star, int Count, int Percent);

    public sealed record RatingSummaryDto(double Average, int Total, IReadOnlyList<StarBarDto> Bars);

    public sealed record PublicReplyDto(string Content, TimeView RepliedAt);

    /// <summary>
    /// 公開卡片。⚠️ 沒有 ReviewId、沒有員工姓名、沒有顧客滿意度——結構上就不給。
    /// </summary>
    public sealed record PublicReviewCardDto(
        byte StarRating,
        string? Content,
        bool IsRatingOnly,
        TimeView CreatedAt,
        bool MentionsVenue,
        bool MentionsStaff,
        string? VenueName,
        string DisplayName,
        PublicReplyDto? Reply);

    public sealed record PublicReviewListDto(
        ReviewFilterDto Filter,
        ReviewDefaultsDto Defaults,
        ReviewOptionsDto Options,
        IReadOnlyList<ViewModels.ReviewVM.SportTabVM> SportTabs,   // 沿用既有的 record，形狀剛好一樣
        RatingSummaryDto Summary,
        IReadOnlyList<PublicReviewCardDto> Items,
        int Page,
        int TotalPages);

    // ── 2-2 撰寫頁設定 ───────────────────────────────────────────

    /// <summary>確認區塊的兩行字（＝ 現在 VM 的 ContextPrimary／ContextSecondary）。</summary>
    public sealed record ReviewContextDto(string? Primary, string? Secondary);

    public sealed record WriteFormInitialDto(bool IsAnonymous, bool IsPublic);

    public sealed record WriteFormOptionsDto(
        bool ShowMentions,
        bool ShowAnonymous,
        bool CanChooseAnonymous,
        bool ShowPublic,
        WriteFormInitialDto Initial,
        int ContentMaxLength);

    public sealed record WriteFormDto(string Kind, ReviewContextDto Context, WriteFormOptionsDto Form);

    // ── 2-4 我的評論 ─────────────────────────────────────────────

    public sealed record MyReplyDto(
        string Content,
        TimeView RepliedAt,
        bool IsViewed,
        byte? Satisfaction,
        bool CanRateSatisfaction);

    /// <summary>⚠️ 沒有 ReviewId。前端用網址上的憑證識別，不需要它。</summary>
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
        bool IsSpamMarked,
        string? SpamReasonText,      // 被下架時給顧客看的理由（ReviewPolicy.SpamReasonInfos 的 CustomerText）；沒下架是 null
        bool CanToggleVisibility,
        MyReplyDto? Reply);

    // ── 2-5 ～ 2-7 的請求與回應 ──────────────────────────────────

    public sealed record ReplyViewedResult(bool Viewed);

    /// <summary>
    /// 用 bool? 而不是 bool：前端忘了送這個欄位時，bool 會默默變成 false（＝ 關掉公開），
    /// bool? 則是 null，後端才分得出「沒送」和「送了 false」。
    /// </summary>
    public sealed record VisibilityRequest(bool? IsPublic);

    public sealed record VisibilityResult(bool IsPublic);

    /// <summary>同理用 byte?：沒送的時候是 null，不會被當成 0（不滿意）。</summary>
    public sealed record SatisfactionRequest(byte? Satisfaction);

    public sealed record SatisfactionResult(byte Satisfaction);
}
