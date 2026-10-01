using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;

namespace VenueGo.Services.Auth;

public class AuthenticationService : IAuthenticationService
{
    private readonly dbVenueContext _db;

    public AuthenticationService(dbVenueContext db)
    {
        _db = db;
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "帳號或密碼錯誤"
            };
        }
        if (user.LockedUntil.HasValue &&
    user.LockedUntil.Value > DateTime.Now)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "帳號目前已被鎖定，請稍後再試"
            };
        }
        // 驗證使用者輸入的密碼
        bool passwordValid = PasswordHelper.VerifyPassword(
            password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            // 登入失敗次數 +1
            user.FailedLoginCount++;

            // 第 5 次失敗後鎖定 15 分鐘
            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = DateTime.Now.AddMinutes(15);
            }

            await _db.SaveChangesAsync();

            return new LoginResult
            {
                Success = false,
                ErrorMessage = "帳號或密碼錯誤"
            };
        }

        // 登入成功，重設登入失敗次數
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        await _db.SaveChangesAsync();

        return new LoginResult
        {
            Success = true
        };
        
    }
}