namespace VenueGo.Dtos.TicketDtos
{
    public class TicketQrDto
    {
        public string Payload { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public string Status { get; set; } = null!;
    }
}
