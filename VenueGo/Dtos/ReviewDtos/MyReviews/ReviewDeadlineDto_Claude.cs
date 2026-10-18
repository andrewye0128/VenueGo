using VenueGo.Dtos;

namespace VenueGo.Dtos.ReviewDtos.MyReviews
{
    /// <summary>
    /// 待評論卡片上的評論期限（10/10）。
    /// 剩餘時間在後端用 ITimeService 算好：時光機撥時間時，畫面上的「剩幾天」會跟資格判定一致。
    /// </summary>
    public sealed record ReviewDeadlineDto(
        TimeView ExpiresAt,          // 期限（完整時間）
        string RemainingText,        // 「剩 3 天」「剩 5 小時」「剩不到 1 小時」
        bool ExpiringSoon);          // 快到期了：卡片用橘色提醒
}
