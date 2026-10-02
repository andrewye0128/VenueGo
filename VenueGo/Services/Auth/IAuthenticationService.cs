namespace VenueGo.Services.Auth;

public interface IAuthenticationService
{
    Task<LoginResult> LoginAsync(string email, string password, string ipAddress);
}