namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    /// <summary>
    /// 送出預約評論。
    /// 預約評論一律不公開、不匿名，所以這裡刻意「沒有」IsAnonymous、IsPublic 兩個欄位：
    /// 以前是收進來之後在 Controller 裡蓋成 false，現在從結構上就收不到。
    /// 目前沒有自己專屬的欄位；保留這個類別，是為了讓 API 文件和 Action 參數一看就知道是哪一種評論。
    /// </summary>
    public sealed class ReviewCreateForBookingDto : ReviewCreateInputDto
    {
    }
}
