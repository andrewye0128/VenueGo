using VenueGo.Models.Entities;

namespace VenueGo.Services.Reviews
{
    /// <summary>
    /// 「待評論」的憑證：本人的、還沒評過、還沒過期。
    /// 現場、預約分開放，因為兩種憑證的欄位不一樣（預約憑證沒有場地、時段）。
    /// </summary>
    public sealed record PendingReviewTickets(
        IReadOnlyList<ReviewPerVisit> Visits,
        IReadOnlyList<ReviewPerBooking> Bookings);
}
