namespace VenueGo.Dtos.MemberDtos
{
    public class MemberProfileResponseDto
    {
        public string UserId { get; set; } = string.Empty;

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Birth { get; set; }

        public string? CarrierNo { get; set; }
    }
}
