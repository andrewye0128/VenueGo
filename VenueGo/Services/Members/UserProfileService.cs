using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using VenueGo.Data;
using VenueGo.Dtos.MemberDtos;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Services.Time;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    public class UserProfileService : IUserProfileService
    {
        private readonly dbVenueContext _db;
        private readonly ILogger<UserProfileService> _logger;
        private readonly ITimeService _time;

        // 手機條碼載具：斜線加 7 碼大寫英數字（與預約流程 ConfirmReservationViewModel 的規則一致）
        private static readonly Regex CarrierNoPattern = new(@"^/[0-9A-Z+\-.]{7}$", RegexOptions.Compiled);

        // 新密碼規則：與後台 UserProfileViewModel.NewPassword 的規則相同（至少 8 碼，含大寫、小寫、數字）
        private const int MinPasswordLength = 8;
        private static readonly Regex PasswordComplexityPattern = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", RegexOptions.Compiled);

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
                var currentPasswordError = CheckCurrentPassword(user, model.CurrentPassword);
                if (currentPasswordError != null)
                {
                    return ServiceResult.ValidationFailed(currentPasswordError.Value.Key, currentPasswordError.Value.Message);
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

        public async Task<ServiceResult> UpdateMemberProfileAsync(int userId, UpdateMemberProfileRequestDto request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return ServiceResult.NotFound();

            // 先把所有驗證做完再改資料（原因同 UpdateProfileAsync：避免驗證失敗時，
            // 同一個 DbContext 裡的實體已經被改過，留下之後誤存的風險）
            var errors = new List<(string Key, string Message)>();

            // 載具：空白代表清除（存 NULL）；有填就轉大寫後檢查格式
            string? carrierNo = string.IsNullOrWhiteSpace(request.CarrierNo)
                ? null
                : request.CarrierNo.Trim().ToUpperInvariant();

            if (carrierNo != null && !CarrierNoPattern.IsMatch(carrierNo))
            {
                errors.Add((nameof(request.CarrierNo), "載具號碼格式不正確，應為斜線加 7 碼大寫英數字，例如 /AB12345。"));
            }

            // 密碼：新密碼留空（null 或 ""）代表不改
            bool changePassword = !string.IsNullOrEmpty(request.NewPassword);
            if (changePassword)
            {
                if (request.NewPassword!.Length < MinPasswordLength)
                {
                    errors.Add((nameof(request.NewPassword), "新密碼長度至少需為 8 個字"));
                }
                else if (!PasswordComplexityPattern.IsMatch(request.NewPassword))
                {
                    errors.Add((nameof(request.NewPassword), "密碼必須包含至少一個大寫英文字母、一個小寫英文字母與一個數字"));
                }

                var currentPasswordError = CheckCurrentPassword(user, request.CurrentPassword);
                if (currentPasswordError != null) errors.Add(currentPasswordError.Value);
            }

            if (errors.Count > 0) return ServiceResult.ValidationFailed(errors);

            var name = request.Name.Trim();
            var phone = request.Phone.Trim();

            // 只記錄「哪些欄位有變」，不把個資內容、密碼寫進稽核紀錄
            var changed = new List<string>();
            if (user.Name != name) changed.Add(nameof(user.Name));
            if (user.Phone != phone) changed.Add(nameof(user.Phone));
            if (user.CarrierNo != carrierNo) changed.Add(nameof(user.CarrierNo));
            if (changePassword) changed.Add("Password");

            // 沒有任何變更：直接當成功，不更新 UpdatedAt、不寫稽核
            if (changed.Count == 0) return ServiceResult.Success();

            user.Name = name;
            user.Phone = phone;
            user.CarrierNo = carrierNo;
            if (changePassword)
            {
                user.PasswordHash = PasswordHelper.HashPassword(request.NewPassword!);
            }
            user.UpdatedAt = _time.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = AuditActions.UpdateProfile,
                EntityType = AuditEntityTypes.User,
                EntityId = user.UserId.ToString(),
                NewValue = $"Member updated profile ({string.Join(", ", changed)}) for {user.Email}",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update member profile for UserId {UserId}", userId);
                return ServiceResult.Error("更新個人資料時發生錯誤，請稍後再試。");
            }

            return ServiceResult.Success();
        }

        /// <summary>
        /// 修改密碼前，確認「舊密碼有填、而且正確」。後台（UpdateProfileAsync）與前台
        /// （UpdateMemberProfileAsync）共用這一份，驗證舊密碼的規則只留一處。
        /// 通過回傳 null；不通過回傳 (欄位名稱, 訊息)。
        /// </summary>
        private static (string Key, string Message)? CheckCurrentPassword(User user, string? currentPassword)
        {
            if (string.IsNullOrEmpty(currentPassword))
            {
                return (nameof(UpdateMemberProfileRequestDto.CurrentPassword), "欲修改密碼，請輸入舊密碼。");
            }

            if (!PasswordHelper.VerifyPassword(currentPassword, user.PasswordHash))
            {
                return (nameof(UpdateMemberProfileRequestDto.CurrentPassword), "舊密碼輸入錯誤。");
            }

            return null;
        }
    }
}