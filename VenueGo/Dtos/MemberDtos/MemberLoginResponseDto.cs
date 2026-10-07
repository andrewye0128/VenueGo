namespace VenueGo.Dtos.MemberDtos
{
    public class MemberLoginResponseDto
    {
        public string Token { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
