namespace VenueGo.Dtos.ReviewDtos
{

    /// <summary>
    /// 撰寫頁、我的評論最上方「確認區塊」的兩行字。
    /// 現場評論：場地名稱／「M/d HH:mm 使用」；預約評論：訂單編號／付款方式。
    /// 兩行字怎麼組在 Mappers/ReviewMapper 的 VisitContext、BookingContext。
    /// 撰寫（WriteReview）和我的評論（MyReviews）都會用到，所以放在 ReviewDtos 根目錄。
    /// </summary>
    public sealed record ReviewContextDto(string? Primary, string? Secondary);
}
