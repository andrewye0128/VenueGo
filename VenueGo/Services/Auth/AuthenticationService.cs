//using Microsoft.EntityFrameworkCore;
//using VenueGo.Data;
//using VenueGo.Helpers;
//using VenueGo.Models.Constants;
//using VenueGo.Models.Entities;

//namespace VenueGo.Services.Auth;

//public class AuthenticationService : IAuthenticationService
//{
//    // LoginLogs.LoginAccount 欄位長度上限（255）。帳號欄位是使用者輸入的原始字串，
//    // 超過上限會讓 SaveChanges 丟例外，變成「亂打一長串帳號就能讓登入頁 500」。
//    private const int MaxLoginAccountLength = 255;

//    private readonly dbVenueContext _db;

//    // 全系統統一的時間來源，不直接用 DateTime.Now。
//    // 帳號鎖定是拿「現在」跟「解鎖時間」比較，寫入與比較必須用同一個時間來源，所以整支一起換。
//    private readonly ITimeService _time;

//    public AuthenticationService(dbVenueContext db, ITimeService time)
//    {
//        _db = db;
//        _time = time;
//    }

//    public async Task<LoginResult> LoginAsync(string email, string password, string ipAddress)
//    {
//        var normalizedEmail = email.Trim().ToLowerInvariant();

//        var user = await _db.Users
//            .FirstOrDefaultAsync(u =>
//                u.Email.ToLower() == normalizedEmail);

//        if (user == null)
//        {
//            await WriteLoginLogAsync(
//             null,
//             email,
//             ipAddress,
//             false,
//             "帳號不存在"
//          );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = "帳號或密碼錯誤"
//            };
//        }
//        // 檢查資料庫鎖定狀態

//        if (user.LockedUntil.HasValue &&
//            user.LockedUntil.Value > _time.Now)
//        {
//            var remainingMinutes = (int)Math.Ceiling(
//                (user.LockedUntil.Value - _time.Now).TotalMinutes
//            );

//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                $"帳號鎖定中（剩餘 {remainingMinutes} 分鐘）"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。"
//            };
//        }
//        else if (user.LockedUntil.HasValue)
//        {
//            // 鎖定時間已過期，重新給予乾淨的嘗試次數
//            user.FailedLoginCount = 0;
//            user.LockedUntil = null;

//            await _db.SaveChangesAsync();
//        }
//        // 驗證使用者輸入的密碼
//        bool passwordValid = PasswordHelper.VerifyPassword(
//            password,
//            user.PasswordHash
//        );

//        if (!passwordValid)
//        {
//            user.FailedLoginCount++;

//            string reason;
//            string errorMessage;

//            if (user.FailedLoginCount >= 5)
//            {
//                user.LockedUntil = _time.Now.AddMinutes(15);

//                reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
//                errorMessage = "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！";
//            }
//            else
//            {
//                int remainingAttempts = 5 - user.FailedLoginCount;

//                reason = $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";
//                errorMessage = $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。";
//            }

//            user.UpdatedAt = _time.Now;

//            await _db.SaveChangesAsync();

//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                reason
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = errorMessage
//            };
//        }

//        // 密碼正確
//        // 後續還需要檢查：
//        // 1. 使用者帳號 Status
//        // 檢查會員帳號狀態

//        if (user.Status != UserStatuses.Active)
//        {
//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                $"使用者帳號已停用（目前狀態：{user.Status}）"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = "此帳號已被停用，請聯繫系統管理員。"
//            };
//        }

//        // 2. 是否為 Employee
//        var employee = await _db.Employees
//        .FirstOrDefaultAsync(e => e.UserId == user.UserId);

//        if (employee == null)
//        {
//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                "此帳號非系統員工帳號"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = "登入失敗：此帳號非系統員工帳號。"
//            };
//        }
//        // 3. Employee Status

//        if (employee.Status != EmployeeStatuses.Active)
//        {
//            string statusText = employee.Status switch
//            {
//                EmployeeStatuses.Resigned => "已離職",
//                EmployeeStatuses.OnLeave => "留職停薪",
//                _ => "狀態異常"
//            };

//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                $"員工帳號狀態異常：{statusText}"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = $"登入失敗：該員工帳號目前為「{statusText}」狀態，無法登入後台。"
//            };
//        }

//        // 4. 是否具有後台角色
//        // 查詢使用者的後台角色
//        var userRoles = await (
//            from ur in _db.UserRoles
//            join r in _db.Roles on ur.RoleId equals r.RoleId
//            where ur.UserId == user.UserId
//                  && r.RoleName != RoleNames.Member
//                  && r.Status // [Role.Status] 只計入啟用中的角色，與 EmployeeAuthorizeFilter 一致
//            select r.RoleName
//        ).ToListAsync();

//        // 沒有後台角色，不能登入後台
//        if (!userRoles.Any())
//        {
//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                "此帳號未具備後台操作權限"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = "登入失敗：此帳號未具備後台操作權限。"
//            };
//        }
//        // 全部通過後，才會真正視為登入成功。

//        // 所有登入條件都通過
//        // 代表真正登入成功

//        user.FailedLoginCount = 0;
//        user.LockedUntil = null;
//        user.LastLoginAt = _time.Now;
//        user.UpdatedAt = _time.Now;

//        await _db.SaveChangesAsync();

//        // 記錄登入成功
//        await WriteLoginLogAsync(
//            user.UserId,
//            email,
//            ipAddress,
//            true,
//            null
//        );

//        return new LoginResult
//        {
//            Success = true,

//            UserId = user.UserId,
//            UserName = user.Name,
//            Email = user.Email,

//            EmployeeId = employee.EmployeeId,
//            EmployeeNo = employee.EmployeeNo,

//            Roles = userRoles
//        };

//    }

//    public async Task<LoginResult> LoginAsMemberAsync(
//    string email,
//    string password,
//    string ipAddress)
//    {
//        var normalizedEmail = email.Trim().ToLowerInvariant();

//        var user = await _db.Users
//            .FirstOrDefaultAsync(u =>
//                u.Email.ToLower() == normalizedEmail);

//        if (user == null)
//        {
//            await WriteLoginLogAsync(
//                null,
//                email,
//                ipAddress,
//                false,
//                "帳號不存在"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = "帳號或密碼錯誤"
//            };
//        }

//        // 檢查帳號是否仍在鎖定期間
//        if (user.LockedUntil.HasValue &&
//            user.LockedUntil.Value > _time.Now)
//        {
//            var remainingMinutes = (int)Math.Ceiling(
//                (user.LockedUntil.Value - _time.Now).TotalMinutes
//            );

//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                $"帳號鎖定中（剩餘 {remainingMinutes} 分鐘）"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage =
//                    $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。"
//            };
//        }

//        // 鎖定時間已過，解除鎖定
//        if (user.LockedUntil.HasValue)
//        {
//            user.FailedLoginCount = 0;
//            user.LockedUntil = null;
//            user.UpdatedAt = _time.Now;

//            await _db.SaveChangesAsync();
//        }

//        // 驗證密碼
//        bool passwordValid = PasswordHelper.VerifyPassword(
//            password,
//            user.PasswordHash
//        );

//        if (!passwordValid)
//        {
//            user.FailedLoginCount++;

//            string reason;
//            string errorMessage;

//            if (user.FailedLoginCount >= 5)
//            {
//                user.LockedUntil = _time.Now.AddMinutes(15);

//                reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
//                errorMessage = "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！";
//            }
//            else
//            {
//                int remainingAttempts =
//                    5 - user.FailedLoginCount;

//                reason =
//                    $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";

//                errorMessage =
//                    $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。";
//            }

//            user.UpdatedAt = _time.Now;

//            await _db.SaveChangesAsync();

//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                reason
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage = errorMessage
//            };
//        }

//        // 會員帳號必須是 Active
//        if (user.Status != UserStatuses.Active)
//        {
//            await WriteLoginLogAsync(
//                user.UserId,
//                email,
//                ipAddress,
//                false,
//                $"會員帳號已停用（目前狀態：{user.Status}）"
//            );

//            return new LoginResult
//            {
//                Success = false,
//                ErrorMessage =
//                    "此帳號已被停用，請聯繫系統管理員。"
//            };
//        }

//        // 登入成功
//        user.FailedLoginCount = 0;
//        user.LockedUntil = null;
//        user.LastLoginAt = _time.Now;
//        user.UpdatedAt = _time.Now;

//        await _db.SaveChangesAsync();

//        await WriteLoginLogAsync(
//            user.UserId,
//            email,
//            ipAddress,
//            true,
//            null
//        );

//        return new LoginResult
//        {
//            Success = true,
//            UserId = user.UserId,
//            UserName = user.Name,
//            Email = user.Email,

//            // 前台會員固定使用 Member 角色
//            Roles = new List<string>
//        {
//            RoleNames.Member
//        },

//            // 會員不是後台員工
//            EmployeeId = null,
//            EmployeeNo = null
//        };
//    }

//    private async Task WriteLoginLogAsync(
//        int? userId,
//        string account,
//        string ipAddress,
//        bool result,
//        string? failureReason)
//    {
//        var safeAccount = account.Length > MaxLoginAccountLength
//            ? account[..MaxLoginAccountLength]
//            : account;

//        var log = new LoginLog
//        {
//            UserId = userId,
//            LoginAccount = safeAccount,
//            IpAddress = ipAddress,
//            LoginTime = _time.Now,
//            Result = result,
//            FailureReason = failureReason
//        };

//        _db.LoginLogs.Add(log);
//        await _db.SaveChangesAsync();
//    }
//}
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;

namespace VenueGo.Services.Auth;

public class AuthenticationService : IAuthenticationService
{
    // LoginLogs.LoginAccount 欄位長度上限（255）。帳號欄位是使用者輸入的原始字串，
    // 超過上限會讓 SaveChanges 丟例外，變成「亂打一長串帳號就能讓登入頁 500」。
    private const int MaxLoginAccountLength = 255;

    private readonly dbVenueContext _db;

    // 全系統統一的時間來源，不直接用 DateTime.Now。
    // 帳號鎖定是拿「現在」跟「解鎖時間」比較，寫入與比較必須用同一個時間來源，所以整支一起換。
    private readonly ITimeService _time;

    public AuthenticationService(dbVenueContext db, ITimeService time)
    {
        _db = db;
        _time = time;
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
                ErrorMessage = "帳號或密碼錯誤",
                ErrorCode = "InvalidCredentials"
            };
        }
        // 檢查資料庫鎖定狀態

        if (user.LockedUntil.HasValue &&
            user.LockedUntil.Value > _time.Now)
        {
            var remainingMinutes = (int)Math.Ceiling(
                (user.LockedUntil.Value - _time.Now).TotalMinutes
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
                ErrorMessage = $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。",
                ErrorCode = "AccountLocked"
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

            string failErrorCode;

            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = _time.Now.AddMinutes(15);

                reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
                errorMessage = "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！";
                failErrorCode = "AccountLocked";
            }
            else
            {
                int remainingAttempts = 5 - user.FailedLoginCount;

                reason = $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";
                errorMessage = $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。";
                failErrorCode = "InvalidCredentials";
            }

            user.UpdatedAt = _time.Now;

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
                ErrorMessage = errorMessage,
                ErrorCode = failErrorCode
            };
        }

        // 密碼正確
        // 後續還需要檢查：
        // 1. 使用者帳號 Status
        // 檢查會員帳號狀態

        if (user.Status != UserStatuses.Active)
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
                ErrorMessage = "此帳號已被停用，請聯繫系統管理員。",
                ErrorCode = user.Status == UserStatuses.Suspended ? "AccountSuspended" : "AccountInactive"
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
                ErrorMessage = "登入失敗：此帳號非系統員工帳號。",
                ErrorCode = "NotEmployeeAccount"
            };
        }
        // 3. Employee Status

        if (employee.Status != EmployeeStatuses.Active)
        {
            string statusText = employee.Status switch
            {
                EmployeeStatuses.Resigned => "已離職",
                EmployeeStatuses.OnLeave => "留職停薪",
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
                ErrorMessage = $"登入失敗：該員工帳號目前為「{statusText}」狀態，無法登入後台。",
                ErrorCode = "EmployeeInactive"
            };
        }

        // 4. 是否具有後台角色
        // 查詢使用者的後台角色
        var userRoles = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.RoleId
            where ur.UserId == user.UserId
                  && r.RoleName != RoleNames.Member
                  && r.Status // [Role.Status] 只計入啟用中的角色，與 EmployeeAuthorizeFilter 一致
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
                ErrorMessage = "登入失敗：此帳號未具備後台操作權限。",
                ErrorCode = "NoBackOfficeAccess"
            };
        }
        // 全部通過後，才會真正視為登入成功。

        // 所有登入條件都通過
        // 代表真正登入成功

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = _time.Now;
        user.UpdatedAt = _time.Now;

        await _db.SaveChangesAsync();

        // 記錄登入成功
        await WriteLoginLogAsync(
            user.UserId,
            email,
            ipAddress,
            true,
            null
        );

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

    public async Task<LoginResult> LoginAsMemberAsync(
    string email,
    string password,
    string ipAddress)
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
                ErrorMessage = "帳號或密碼錯誤",
                ErrorCode = "InvalidCredentials"
            };
        }

        // 檢查帳號是否仍在鎖定期間
        if (user.LockedUntil.HasValue &&
            user.LockedUntil.Value > _time.Now)
        {
            var remainingMinutes = (int)Math.Ceiling(
                (user.LockedUntil.Value - _time.Now).TotalMinutes
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
                ErrorMessage =
                    $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。",
                ErrorCode = "AccountLocked"
            };
        }

        // 鎖定時間已過，解除鎖定
        if (user.LockedUntil.HasValue)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = _time.Now;

            await _db.SaveChangesAsync();
        }

        // 驗證密碼
        bool passwordValid = 
            PasswordHelper.VerifyPassword(
            password,
            user.PasswordHash
        );

        if (!passwordValid)
        {
            user.FailedLoginCount++;

            string reason;
            string errorMessage;

            string failErrorCode;

            if (user.FailedLoginCount >= 5)
            {
                user.LockedUntil = _time.Now.AddMinutes(15);

                reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
                errorMessage = "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！";
                failErrorCode = "AccountLocked";
            }
            else
            {
                int remainingAttempts =
                    5 - user.FailedLoginCount;

                reason =
                    $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";

                errorMessage =
                    $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。";
                failErrorCode = "InvalidCredentials";
            }

            user.UpdatedAt = _time.Now;

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
                ErrorMessage = errorMessage,
                ErrorCode = failErrorCode
            };
        }

        // 會員帳號必須是 Active
        if (user.Status != UserStatuses.Active)
        {
            await WriteLoginLogAsync(
                user.UserId,
                email,
                ipAddress,
                false,
                $"會員帳號已停用（目前狀態：{user.Status}）"
            );

            string statusErrorCode = user.Status switch
            {
                UserStatuses.Suspended => "AccountSuspended",
                UserStatuses.Inactive => "AccountInactive",
                _ => "AccountInactive"
            };

            string statusErrorMessage = user.Status switch
            {
                UserStatuses.Suspended => "此帳號已被停權，如有疑問請聯繫客服。",
                UserStatuses.Inactive => "此帳號已註銷，如需使用請重新註冊。",
                _ => "此帳號狀態異常，請聯繫客服。"
            };

            return new LoginResult
            {
                Success = false,
                ErrorMessage = statusErrorMessage,
                ErrorCode = statusErrorCode
            };
        }

        // 登入成功
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = _time.Now;
        user.UpdatedAt = _time.Now;

        await _db.SaveChangesAsync();

        await WriteLoginLogAsync(
            user.UserId,
            email,
            ipAddress,
            true,
            null
        );

        return new LoginResult
        {
            Success = true,
            UserId = user.UserId,
            UserName = user.Name,
            Email = user.Email,

            // 前台會員固定使用 Member 角色
            Roles = new List<string>
        {
            RoleNames.Member
        },

            // 會員不是後台員工
            EmployeeId = null,
            EmployeeNo = null
        };
    }

    private async Task WriteLoginLogAsync(
        int? userId,
        string account,
        string ipAddress,
        bool result,
        string? failureReason)
    {
        var safeAccount = account.Length > MaxLoginAccountLength
            ? account[..MaxLoginAccountLength]
            : account;

        var log = new LoginLog
        {
            UserId = userId,
            LoginAccount = safeAccount,
            IpAddress = ipAddress,
            LoginTime = _time.Now,
            Result = result,
            FailureReason = failureReason
        };

        _db.LoginLogs.Add(log);
        await _db.SaveChangesAsync();
    }
}