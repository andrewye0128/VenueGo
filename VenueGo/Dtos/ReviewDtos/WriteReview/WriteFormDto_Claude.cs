namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    /// <summary>
    /// 撰寫頁設定（規格 2-2）。Kind 是 ReviewKind 的值（visit／booking）。
    /// ⚠️ 屬性名稱就是 JSON 的欄位名稱，改名等於改規格，前端要跟著改。
    /// </summary>
    public sealed record WriteFormDto(string Kind, ReviewContextDto Context, WriteFormOptionsDto Form);
}
