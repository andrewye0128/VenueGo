namespace VenueGo.Services
{
    public interface ICurrentUser
    {
        int? MemberId { get; }
        int? EmployeeId { get; }
    }
}
