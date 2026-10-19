namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{

    /// <summary>
    /// 平均分數、總則數、星等分布。
    /// ⚠️ 用「套用星等篩選之前」的結果算，切換星等時分布圖不會變。
    /// </summary>
    public sealed record RatingSummaryDto(double Average, int Total, IReadOnlyList<StarBarDto> Bars);
}
