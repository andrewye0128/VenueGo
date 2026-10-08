using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    public class UserProfileService : IUserProfileService
    {
        private readonly dbVenueContext _db;
        private readonly ILogger<UserProfileService> _logger;
        private readonly ITimeService _time;

        public UserProfileService(dbVenueContext db, ILogger<UserProfileService> logger, ITimeService time)
        {
            _db = db;
            _logger = logger;
            _time = time;
        }

        public async Task<UserProfileViewModel?> GetProfileAsync(int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return null;

            // 一般會員沒有 Employees 資料，所以是 Left Join 的概念
            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);

            var roleNames = await (from ur in _db.UserRoles
                                   join r in _db.Roles on ur.RoleId equals r.RoleId
                                   where ur.UserId == userId
                                   select r.RoleName).ToListAsync();

            return new UserProfileViewModel
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                EmployeeNo = employee?.EmployeeNo ?? "無",
                JobTitle = employee?.JobTitle ?? "一般會員",
                Roles = roleNames
            };
        }

        public async Task<ServiceResult> UpdateProfileAsync(int userId, UserProfileViewModel model)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return ServiceResult.NotFound();

            // 先把密碼驗證做完，通過了才開始改資料：
            // 原本是先改 user.Name / Phone 再驗密碼，驗證失敗時雖然沒存檔，
            // 但同一個 DbContext 裡的實體已經是被改過的狀態，容易埋下之後誤存的風險。
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (string.IsNullOrEmpty(model.CurrentPassword))
                {
                    return ServiceResult.ValidationFailed(nameof(model.CurrentPassword), "欲修改密碼，請輸入舊密碼。");
                }

                if (!PasswordHelper.VerifyPassword(model.CurrentPassword, user.PasswordHash))
                {
                    return ServiceResult.ValidationFailed(nameof(model.CurrentPassword), "舊密碼輸入錯誤。");
                }

                user.PasswordHash = PasswordHelper.HashPassword(model.NewPassword);
            }

            user.Name = model.Name.Trim();
            user.Phone = model.Phone.Trim();
            user.UpdatedAt = _time.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditActions.UpdateProfile,
                EntityType = AuditEntityTypes.User,
                EntityId = user.UserId.ToString(),
                NewValue = $"Updated profile for {user.Email}",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update profile for UserId {UserId}", userId);
                return ServiceResult.Error("更新個人資料時發生錯誤，請稍後再試。");
            }

            return ServiceResult.Success();
        }
    }
}