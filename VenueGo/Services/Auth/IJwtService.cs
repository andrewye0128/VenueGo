namespace VenueGo.Services.Auth;

public interface IJwtService
{
    string GenerateToken(LoginResult loginResult);
}