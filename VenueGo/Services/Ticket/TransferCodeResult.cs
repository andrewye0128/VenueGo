namespace VenueGo.Services.Ticket
{
    public record TransferCodeResult(bool Success, string? Reason, string? Code, DateTime? ExpiresAt)
    {
        public static TransferCodeResult Ok(string code, DateTime expiresAt) => new(true, null, code, expiresAt);
        public static TransferCodeResult Fail(string reason) => new(false, reason, null, null);
    }
}
