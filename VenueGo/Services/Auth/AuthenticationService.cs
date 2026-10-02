using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;

namespace VenueGo.Services.Auth;

public class AuthenticationService : IAuthenticationService
{
    private readonly dbVenueContext _db;

    public AuthenticationService(dbVenueContext db)
    {
        _db = db;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string ipAddress)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedEmail);

            if (user == null)
            {
              await WriteLoginLogAsync(
               null,
               email,
               ipAddress,
               false,
               "帳號不存在"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = "帳號或密碼錯誤"
            };
        }
        // 檢查資料庫鎖定狀態

        if (user.LockedUntil.HasValue &&
            user.LockedUntil.Value > DateTime.Now)
        {
            var remainingMinutes = (int)Math.Ceiling(
                (user.LockedUntil.Value - DateTime.Now).TotalMinutes
            );

            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                $"帳號鎖定中（剩餘 {remainingMinutes} 分鐘）"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。"
            };
        }
        else if (user.LockedUntil.HasValue)
        {
            // 鎖定時間已過期，重新給予乾淨的嘗試次數
            user.FailedLoginCount = 0;
            user.LockedUntil = null;

            await _db.SaveChangesAsync();
        }
        // 驗證使用者輸入的密碼
        bool passwordValid = PasswordHelper.VerifyPassword(
            password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            user.FailedLoginCount++;

            string reason;
            string errorMessage;

            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = DateTime.Now.AddMinutes(15);

                reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
                errorMessage = "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！";
            }
            else
            {
                int remainingAttempts = 5 - user.FailedLoginCount;

                reason = $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";
                errorMessage = $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。";
            }

            user.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                reason
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        // 密碼正確
        // 後續還需要檢查：
        // 1. 使用者帳號 Status
        // 檢查會員帳號狀態

        if (user.Status != "Active")
        {
            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                $"使用者帳號已停用（目前狀態：{user.Status}）"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = "此帳號已被停用，請聯繫系統管理員。"
            };
        }

        // 2. 是否為 Employee
        var employee = await _db.Employees
        .FirstOrDefaultAsync(e => e.UserId == user.UserId);

        if (employee == null)
        {
            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                "此帳號非系統員工帳號"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = "登入失敗：此帳號非系統員工帳號。"
            };
        }
        // 3. Employee Status

        if (employee.Status != "Active")
        {
            string statusText = employee.Status switch
            {
                "Resigned" => "已離職",
                "OnLeave" => "留職停薪",
                _ => "狀態異常"
            };

            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                $"員工帳號狀態異常：{statusText}"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = $"登入失敗：該員工帳號目前為「{statusText}」狀態，無法登入後台。"
            };
        }

        // 4. 是否具有後台角色
        // 查詢使用者的後台角色
        var userRoles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.RoleId
            where ur.UserId == user.UserId
                  && r.RoleName != "Member"
            select r.RoleName
        ).ToListAsync();

        // 沒有後台角色，不能登入後台
        if (!userRoles.Any())
        {
            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                "此帳號未具備後台操作權限"
            );

            return new LoginResult
            {
                Success = false,
                ErrorMessage = "登入失敗：此帳號未具備後台操作權限。"
            };
        }
        // 全部通過後，才會真正視為登入成功。

        // 所有登入條件都通過
        // 代表真正登入成功

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.Now;
        user.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();

        return new LoginResult
        {
            Success = true,

            UserId = user.UserId,
            UserName = user.Name,
            Email = user.Email,

            EmployeeId = employee.EmployeeId,
            EmployeeNo = employee.EmployeeNo,

            Roles = userRoles
        };

    }

    private async Task WriteLoginLogAsync(
        int? userId,
        string account,
        string ipAddress,
        bool result,
        string? failureReason)
    {
        var log = new LoginLog
        {
            UserId = userId,
            LoginAccount = account,
            IpAddress = ipAddress,
            LoginTime = DateTime.Now,
            Result = result,
            FailureReason = failureReason
        };

        _db.LoginLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}