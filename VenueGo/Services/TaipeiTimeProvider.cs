namespace VenueGo.Services
{
    /// <summary>跟系統時鐘走，但「本地時區」固定是台北，不管伺服器設在哪個時區。</summary>
    public sealed class TaipeiTimeProvider : TimeProvider
    {
        private static readonly TimeZoneInfo Taipei = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        public override TimeZoneInfo LocalTimeZone => Taipei;
    }
}
