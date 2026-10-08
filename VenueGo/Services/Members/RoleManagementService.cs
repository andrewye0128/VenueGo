using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    public class RoleManagementService : IRoleManagementService
    {
        private readonly dbVenueContext _db;
        private readonly ILogger<RoleManagementService> _logger;

        // 全系統統一的時間來源（套用校時偏移量、已捨去毫秒以符合 datetime2(0)），
        // 不直接用 DateTime.Now，才能跟預約、評論等其他模組的時間一致。
        private readonly ITimeService _time;

        // [搬移自 SettingController] 系統保留角色名稱，改用 RoleNames 常數組成，
        // 不再是獨立的字串陣列，避免跟 RoleNames.cs 各自維護、哪天拼法對不起來。
        private static readonly string[] ReservedRoleNames = { RoleNames.Admin, RoleNames.Member };

        public RoleManagementService(dbVenueContext db, ILogger<RoleManagementService> logger, ITimeService time)
        {
            _db = db;
            _logger = logger;
            _time = time;
        }

        // ============================================================
        // 查詢類方法：單純讀資料給畫面用，不含驗證規則，直接回傳資料或 null
        // ============================================================

        public async Task<List<RoleListItemViewModel>> GetRolesOverviewAsync()
        {
            var roles = await _db.Roles.AsNoTracking().ToListAsync();
            var roleIds = roles.Select(r => r.RoleId).ToList();

            var userCounts = await _db.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            var rolePermissionsMap = await (from rp in _db.RolePermissions
                                            join p in _db.Permissions on rp.PermissionId equals p.PermissionId
                                            where roleIds.Contains(rp.RoleId) && rp.Status && p.Status
                                            select new { rp.RoleId, p.PermissionName })
                                            .ToListAsync();

            return roles.Select(r => new RoleListItemViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description,
                Status = r.Status,
                UserCount = userCounts.GetValueOrDefault(r.RoleId, 0),
                PermissionNames = rolePermissionsMap
                    .Where(rp => rp.RoleId == r.RoleId)
                    .Select(rp => rp.PermissionName)
                    .ToList()
            }).ToList();
        }

        public async Task<RoleEditViewModel?> GetRoleForEditAsync(int roleId)
        {
            var role = await _db.Roles.FindAsync(roleId);
            if (role == null) return null;

            var selectedPermIds = await _db.RolePermissions
                .Where(rp => rp.RoleId == roleId && rp.Status)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            return new RoleEditViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description,
                Status = role.Status,
                SelectedPermissionIds = selectedPermIds,
                AvailablePermissions = await GetAvailablePermissionsAsync()
            };
        }

        public async Task<RoleCreateViewModel> GetRoleForCreateAsync()
        {
            return new RoleCreateViewModel
            {
                AvailablePermissions = await GetAvailablePermissionsAsync()
            };
        }

        public async Task<List<PermissionOptionDto>> GetAvailablePermissionsAsync()
        {
            return await _db.Permissions
                .Where(p => p.Status)
                .Select(p => new PermissionOptionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionCode = p.PermissionCode,
                    PermissionName = p.PermissionName,
                    Description = p.Description,
                    Status = p.Status
                }).ToListAsync();
        }

        // ============================================================
        // 寫入類方法：含驗證規則 + 交易 + 稽核紀錄，回傳 ServiceResult
        // ============================================================

        public async Task<ServiceResult> UpdateRoleAsync(RoleEditViewModel model, int currentUserId)
        {
            var role = await _db.Roles.FindAsync(model.RoleId);
            if (role == null) return ServiceResult.NotFound();

            bool isReservedRole = ReservedRoleNames.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase);
            var trimmedName = model.RoleName?.Trim() ?? string.Empty;
            bool nameChanged = !string.Equals(role.RoleName, trimmedName, StringComparison.Ordinal);

            var errors = new List<(string Key, string Message)>();

            // [5] 禁止把系統保留角色改名，避免依賴角色名稱的邏輯悄悄失效
            if (isReservedRole && nameChanged)
            {
                errors.Add(("RoleName", $"系統角色「{role.RoleName}」的名稱不可修改。"));
            }

            // [5] 禁止把其他角色改名成與保留角色相同的名稱
            if (!isReservedRole && ReservedRoleNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(("RoleName", "此名稱為系統保留角色名稱，請使用其他名稱。"));
            }

            // 應用層檢查角色名稱是否與其他既有角色重複（排除自己）
            if (!isReservedRole && nameChanged
                && await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: model.RoleId))
            {
                errors.Add(("RoleName", "角色名稱已存在，請使用其他名稱。"));
            }

            // 若此角色已有使用者使用，禁止修改角色名稱（權限仍可調整）
            if (!isReservedRole && nameChanged && await IsRoleInUseAsync(model.RoleId))
            {
                errors.Add(("RoleName", "此角色已有使用者使用，無法修改角色名稱。若需調整權限，請保留原名稱後再送出。"));
            }

            // [7] 禁止停用 Admin 角色本身，避免整個系統沒有人可以再進入後台
            if (string.Equals(role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase) && !model.Status)
            {
                errors.Add(("", "系統管理員角色（Admin）不可被停用。"));
            }

            // 去重，避免同一個 PermissionId 被勾兩次時，後面 RolePermissions.Add 寫入時撞到主鍵
            var selectedPermissionIds = model.SelectedPermissionIds?.Distinct().ToList() ?? new List<int>();

            // [補修正] 原本 CreateRole 有驗證、EditRole 沒有的缺口：
            // 確認送進來的 PermissionId 全部都是真實存在且啟用中的權限，
            // 避免繞過前端畫面，直接送出已停用或不存在的 PermissionId。
            if (selectedPermissionIds.Any())
            {
                int validCount = await _db.Permissions
                    .CountAsync(p => selectedPermissionIds.Contains(p.PermissionId) && p.Status);

                if (validCount != selectedPermissionIds.Count)
                {
                    errors.Add(("", "選擇的權限中包含不存在或已停用的權限。"));
                }
            }

            // [7] 若目前登入者本身持有此角色，且移除權限後會導致自己失去管理員的所有權限，則擋下
            // 保留原本「只有前面都沒有錯誤時才檢查」的行為
            if (!errors.Any())
            {
                var currentUserHasThisRole = await _db.UserRoles
                    .AnyAsync(ur => ur.UserId == currentUserId && ur.RoleId == model.RoleId);

                if (currentUserHasThisRole && string.Equals(role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    bool willHaveAdminPermission = selectedPermissionIds.Any();
                    if (!willHaveAdminPermission)
                    {
                        errors.Add(("", "不可將自己所屬的管理員角色權限全部移除，以免無法再存取設定頁面。"));
                    }
                }
            }

            if (errors.Any())
            {
                return ServiceResult.ValidationFailed(errors);
            }

            role.RoleName = trimmedName;
            role.Description = model.Description;
            role.Status = model.Status;
            role.UpdatedAt = _time.Now;

            var existingRPs = await _db.RolePermissions.Where(rp => rp.RoleId == model.RoleId).ToListAsync();
            _db.RolePermissions.RemoveRange(existingRPs);

            foreach (var permId in selectedPermissionIds)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = model.RoleId,
                    PermissionId = permId,
                    Status = true,
                    AssignedAt = _time.Now,
                    AssignedBy = currentUserId
                });
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = AuditActions.UpdateRolePermissions,
                EntityType = AuditEntityTypes.Role,
                EntityId = model.RoleId.ToString(),
                NewValue = $"Updated Role: {role.RoleName}",
                CreatedAt = _time.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update role permissions for RoleId {RoleId}", model.RoleId);
                return ServiceResult.Error("更新角色權限時發生錯誤，請稍後再試。");
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> CreateRoleAsync(RoleCreateViewModel model, int currentUserId)
        {
            var trimmedName = model.RoleName?.Trim() ?? string.Empty;
            var errors = new List<(string Key, string Message)>();

            // 用 trim 後的名稱做重複性檢查，避免「Staff 」通過檢查但存入後變成重複
            if (await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: null))
            {
                errors.Add(("RoleName", "角色名稱已存在"));
            }

            // [5] 新建角色也不可佔用系統保留名稱
            if (ReservedRoleNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(("RoleName", "此名稱為系統保留角色名稱，請使用其他名稱。"));
            }

            var selectedPermissionIds = model.SelectedPermissionIds?.Distinct().ToList() ?? new List<int>();

            if (selectedPermissionIds.Any())
            {
                int validCount = await _db.Permissions
                    .CountAsync(p => selectedPermissionIds.Contains(p.PermissionId) && p.Status);

                if (validCount != selectedPermissionIds.Count)
                {
                    errors.Add(("", "選擇的權限中包含不存在或已停用的權限。"));
                }
            }

            if (errors.Any())
            {
                return ServiceResult.ValidationFailed(errors);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // 交易內再次確認名稱未被搶先建立（縮小應用層檢查與寫入之間的競態窗口）
                if (await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: null))
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.ValidationFailed("RoleName", "角色名稱已存在，請使用其他名稱。");
                }

                var role = new Role
                {
                    RoleName = trimmedName,
                    Description = model.Description,
                    Status = model.Status,
                    CreatedAt = _time.Now,
                    UpdatedAt = _time.Now
                };

                _db.Roles.Add(role);
                await _db.SaveChangesAsync();

                foreach (var permId in selectedPermissionIds)
                {
                    _db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.RoleId,
                        PermissionId = permId,
                        Status = true,
                        AssignedAt = _time.Now,
                        AssignedBy = currentUserId
                    });
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = AuditActions.CreateRole,
                    EntityType = AuditEntityTypes.Role,
                    EntityId = role.RoleId.ToString(),
                    NewValue = $"Created Role: {role.RoleName}",
                    CreatedAt = _time.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create role {RoleName}", trimmedName);
                return ServiceResult.Error("新增角色失敗，請稍後再試。");
            }
        }

        // ============================================================
        // Private Helpers
        // ============================================================

        private async Task<bool> IsRoleNameDuplicateAsync(string roleName, int? excludeRoleId)
        {
            if (string.IsNullOrWhiteSpace(roleName)) return false;

            var normalized = roleName.Trim().ToLower();

            var query = _db.Roles.Where(r => r.RoleName.ToLower() == normalized);

            if (excludeRoleId.HasValue)
            {
                query = query.Where(r => r.RoleId != excludeRoleId.Value);
            }

            return await query.AnyAsync();
        }

        private async Task<bool> IsRoleInUseAsync(int roleId)
        {
            return await _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId);
        }
    }
}