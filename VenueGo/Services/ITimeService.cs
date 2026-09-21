namespace VenueGo.Services
{
    public interface ITimeService
    {
        /// <summary>
        /// 獲取特定時區的精準當前時間，已剔除毫秒以符合 datetime2(0)
        /// </summary>
        Task<DateTime> GetCurrentTimeAsync(string timeZone = "Asia/Taipei");
    }
}
