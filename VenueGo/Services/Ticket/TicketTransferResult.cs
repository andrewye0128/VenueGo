namespace VenueGo.Services.Ticket
{
    public record TicketTransferResult(bool Success, string? Reason)
    {
        public static TicketTransferResult Ok() => new(true, null);
        public static TicketTransferResult Fail(string reason) => new(false, reason);
    }
}
