using VenueGo.Enums;

namespace VenueGo.Services.Ticket
{
    public record QrVerifyResult(bool Success, int? TicketId, QrVerifyFailReason? Reason)
    {
        public static QrVerifyResult Ok(int ticketId) => new(true, ticketId, null);

        public static QrVerifyResult Fail(QrVerifyFailReason reason) => new(false, null, reason);
    }
}
