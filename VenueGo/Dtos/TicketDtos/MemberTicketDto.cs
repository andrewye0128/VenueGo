namespace VenueGo.Dtos.TicketDtos
{
    public class MemberTicketDto
    {
        public int Id { get; set; }
        public string VenueName { get; set; } = null!;
        public string SportType { get; set; } = null!;
        public string Date { get; set; } = null!;
        public string TimeRange { get; set; } = null!;
        public string Status { get; set; } = null!;     // available / used / expired / transferred
        public string? QrToken { get; set; }            // 只有 available 才給
    }
}
