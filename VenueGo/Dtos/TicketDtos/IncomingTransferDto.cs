namespace VenueGo.Dtos.TicketDtos
{
    public class IncomingTransferDto
    {
        public string FromName { get; set; } = null!;
        public string VenueName { get; set; } = null!;
        public string SportType { get; set; } = null!;
        public string Date { get; set; } = null!;
        public string TimeRange { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
