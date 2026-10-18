namespace VenueGo.Dtos.TicketDtos
{
    public class MemberTicketDto
    {
        public int Id { get; set; }
        public string VenueName { get; set; } = null!;
        public string SportType { get; set; } = null!;
        public string Date { get; set; } = null!;
        public string TimeRange { get; set; } = null!;
        public string Status { get; set; } = null!;      // available / inside / used / expired / transferred

        // 轉發
        public string? TransferCode { get; set; }          // 只有持有人本人看得到
        public DateTime? TransferExpiresAt { get; set; }
    }
}
