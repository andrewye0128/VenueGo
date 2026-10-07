using VenueGo.Models.Enums;

namespace VenueGo.Services.CheckIn
{
    /// <summary>
    /// 報到審核結果。Success 為 true 時 Reason 一定是 null；為 false 時 Reason 一定有值。
    /// </summary>
    public record CheckInResult(bool Success, CheckInFailReason? Reason)
    {
        public static CheckInResult Ok() => new(true, null);
        public static CheckInResult Fail(CheckInFailReason reason) => new(false, reason);
    }
}