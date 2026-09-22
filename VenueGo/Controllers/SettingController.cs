using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.ViewModels;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Controllers
{
    [EmployeeAuthorize("Admin")] // 僅限管理員存取
    public class SettingController : Controller
    {
        private readonly dbVenueContext _db;
        private readonly ILogger<SettingController> _logger; // [4] 加入 Logger

        // [5] 集中管理系統保留角色名稱，避免各處硬編碼字串散落
        // 目前系統角色為：Member（一般會員）、Staff（員工）、Manager（場館營運管理者）、Admin（系統管理員）
        // 其中 Member 與 Admin 有特殊邏輯（自動指派 / 不可被停用等），Staff、Manager 為一般員工角色。
        private static readonly string[] ReservedRoleNames = { "Admin", "Member" };
        private const string AdminRoleName = "Admin";
        private const string MemberRoleName = "Member";

        // [2] 員工狀態白名單，避免任意字串寫入
        private static readonly HashSet<string> AllowedEmployeeStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Active", "Resigned", "OnLeave"
        };

        // [3] 分頁大小上下限
        private const int MinPageSize = 1;
        private const int MaxPageSize = 100;

        public SettingController(dbVenueContext db, ILogger<SettingController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Roles));
        }

        #region 1. 角色權限管理 (Roles)

        public async Task<IActionResult> Roles()
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

            var model = roles.Select(r => new RoleListItemViewModel
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

            return View(model);
        }

        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _db.Roles.FindAsync(id);
            if (role == null) return NotFound();

            var selectedPermIds = await _db.RolePermissions
                .Where(rp => rp.RoleId == id && rp.Status)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var model = new RoleEditViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description,
                Status = role.Status,
                SelectedPermissionIds = selectedPermIds,
                AvailablePermissions = await GetAvailablePermissionsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(RoleEditViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            var role = await _db.Roles.FindAsync(model.RoleId);
            if (role == null) return NotFound();

            bool isReservedRole = ReservedRoleNames.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase);

            // [5] 禁止把系統保留角色改名，避免依賴角色名稱的邏輯悄悄失效
            if (isReservedRole && !string.Equals(role.RoleName, model.RoleName?.Trim(), StringComparison.Ordinal))
            {
                ModelState.AddModelError("RoleName", $"系統角色「{role.RoleName}」的名稱不可修改。");
            }

            // [5] 禁止把其他角色改名成與保留角色相同的名稱
            if (!isReservedRole && ReservedRoleNames.Contains(model.RoleName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("RoleName", "此名稱為系統保留角色名稱，請使用其他名稱。");
            }

            // [7] 禁止停用 Admin 角色本身，避免整個系統沒有人可以再進入後台
            if (string.Equals(role.RoleName, AdminRoleName, StringComparison.OrdinalIgnoreCase) && !model.Status)
            {
                ModelState.AddModelError("", "系統管理員角色（Admin）不可被停用。");
            }

            // [7] 若目前登入者本身持有此角色，且移除權限後會導致自己失去管理員的所有權限，則擋下
            if (ModelState.IsValid)
            {
                var currentUserHasThisRole = await _db.UserRoles
                    .AnyAsync(ur => ur.UserId == currentUserId && ur.RoleId == model.RoleId);

                if (currentUserHasThisRole && string.Equals(role.RoleName, AdminRoleName, StringComparison.OrdinalIgnoreCase))
                {
                    var willHaveAdminPermission = model.SelectedPermissionIds != null && model.SelectedPermissionIds.Any();
                    if (!willHaveAdminPermission)
                    {
                        ModelState.AddModelError("", "不可將自己所屬的管理員角色權限全部移除，以免無法再存取設定頁面。");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }

            role.RoleName = model.RoleName.Trim();
            role.Description = model.Description;
            role.Status = model.Status;
            role.UpdatedAt = DateTime.Now;

            var existingRPs = await _db.RolePermissions.Where(rp => rp.RoleId == model.RoleId).ToListAsync();
            _db.RolePermissions.RemoveRange(existingRPs);

            if (model.SelectedPermissionIds != null && model.SelectedPermissionIds.Any())
            {
                foreach (var permId in model.SelectedPermissionIds)
                {
                    _db.RolePermissions.Add(new RolePermission
                    {
                        RoleId = model.RoleId,
                        PermissionId = permId,
                        Status = true,
                        AssignedAt = DateTime.Now,
                        AssignedBy = currentUserId
                    });
                }
            }

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "UpdateRolePermissions",
                EntityType = "Role",
                EntityId = model.RoleId.ToString(),
                NewValue = $"Updated Role: {model.RoleName}",
                CreatedAt = DateTime.Now
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update role permissions for RoleId {RoleId}", model.RoleId);
                ModelState.AddModelError("", "更新角色權限時發生錯誤，請稍後再試。");
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = "角色權限已成功更新！";

            return RedirectToAction(nameof(Roles));
        }

        public async Task<IActionResult> CreateRole()
        {
            var model = new RoleCreateViewModel
            {
                AvailablePermissions = await GetAvailablePermissionsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleCreateViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (await _db.Roles.AnyAsync(r => r.RoleName == model.RoleName))
            {
                ModelState.AddModelError("RoleName", "角色名稱已存在");
            }

            // [5] 新建角色也不可佔用系統保留名稱
            if (ReservedRoleNames.Contains(model.RoleName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("RoleName", "此名稱為系統保留角色名稱，請使用其他名稱。");
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var role = new Role
                {
                    RoleName = model.RoleName.Trim(),
                    Description = model.Description,
                    Status = model.Status,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _db.Roles.Add(role);
                await _db.SaveChangesAsync();

                if (model.SelectedPermissionIds != null && model.SelectedPermissionIds.Any())
                {
                    foreach (var permId in model.SelectedPermissionIds)
                    {
                        _db.RolePermissions.Add(new RolePermission
                        {
                            RoleId = role.RoleId,
                            PermissionId = permId,
                            Status = true,
                            AssignedAt = DateTime.Now,
                            AssignedBy = currentUserId
                        });
                    }
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "CreateRole",
                    EntityType = "Role",
                    EntityId = role.RoleId.ToString(),
                    NewValue = $"Created Role: {role.RoleName}",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"成功建立新角色：{role.RoleName}";
                return RedirectToAction(nameof(Roles));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create role {RoleName}", model.RoleName); // [4]
                ModelState.AddModelError("", "新增角色失敗，請稍後再試。");
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }
        }

        #endregion

        #region 2. 員工管理 (Employee List & Operations)

        // GET: Setting/UserList
        public async Task<IActionResult> UserList(string? keyword, int? roleId, string? status, int page = 1, int pageSize = 10)
        {
            // [3] 限制 pageSize 範圍，避免一次撈出過大資料集
            pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);
            page = Math.Max(1, page);

            var availableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);

            var baseQuery = from e in _db.Employees
                            join u in _db.Users on e.UserId equals u.UserId
                            select new { Usr = u, Emp = e };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var trimKeyword = keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(x =>
                    x.Usr.Name.ToLower().Contains(trimKeyword) ||
                    x.Usr.Email.ToLower().Contains(trimKeyword) ||
                    x.Emp.EmployeeNo.ToLower().Contains(trimKeyword)
                );
            }

            if (roleId.HasValue && roleId.Value > 0)
            {
                baseQuery = baseQuery.Where(x => _db.UserRoles.Any(ur => ur.UserId == x.Usr.UserId && ur.RoleId == roleId.Value));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(x => x.Emp.Status == status);
            }

            // 1. 計算總筆數與總頁數
            var totalCount = await baseQuery.CountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            // 2. 加入排序與分頁 Skip/Take
            var rawList = await baseQuery
                .OrderBy(x => x.Emp.EmployeeNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Usr.UserId,
                    x.Usr.Name,
                    x.Usr.Email,
                    x.Usr.Phone,
                    x.Emp.EmployeeNo,
                    x.Emp.JobTitle,
                    Status = x.Emp.Status,
                    x.Usr.CreatedAt
                }).ToListAsync();

            var userIds = rawList.Select(u => u.UserId).ToList();
            var userRolesMap = await (from ur in _db.UserRoles
                                      join r in _db.Roles on ur.RoleId equals r.RoleId
                                      where userIds.Contains(ur.UserId)
                                      select new { ur.UserId, r.RoleName })
                                     .ToListAsync();

            var usersList = rawList.Select(u => new UserListItemDto
            {
                UserId = u.UserId,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone ?? "未提供",
                EmployeeNo = u.EmployeeNo,
                JobTitle = u.JobTitle ?? "無",
                IsEmployee = true,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                Roles = userRolesMap.Where(ur => ur.UserId == u.UserId).Select(ur => ur.RoleName).ToList()
            }).ToList();

            var model = new UserListViewModel
            {
                Keyword = keyword,
                SelectedRoleId = roleId,
                SelectedStatus = status,
                SelectedUserType = "employee",
                AvailableRoles = availableRoles,
                Users = usersList,
                // 新增分頁資訊至 ViewModel
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            var model = new RegisterUserViewModel
            {
                AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true),
                EmployeeNo = await GenerateNextEmployeeNoAsync(),
                Birth = DateOnly.FromDateTime(DateTime.Now)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterUserViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            // [6] Email 唯一性檢查改為不分大小寫，避免 A@b.com / a@b.com 視為不同帳號
            var normalizedEmail = model.Email?.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                ModelState.AddModelError("Email", "此 Email 已被註冊使用");
            }

            string finalEmployeeNo = model.EmployeeNo;
            if (string.IsNullOrWhiteSpace(finalEmployeeNo) || await _db.Employees.AnyAsync(e => e.EmployeeNo == finalEmployeeNo))
            {
                finalEmployeeNo = await GenerateNextEmployeeNoAsync();
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                model.EmployeeNo = finalEmployeeNo;
                return View(model);
            }

            // [1] 資料庫已對 EmployeeNo 設定 unique constraint，
            // 這裡加上重試機制：若因併發衝突造成編號重複而寫入失敗，重新產生編號後再試一次。
            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    var user = new User
                    {
                        Email = normalizedEmail,
                        PasswordHash = PasswordHelper.HashPassword(model.Password),
                        Name = model.Name.Trim(),
                        Phone = model.Phone?.Trim(),
                        Status = "Active",
                        Birth = model.Birth,
                        CumulativeConsumption = 0,
                        CumulativeVisitTime = 0,
                        FailedLoginCount = 0,
                        NoShowCount = 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _db.Users.Add(user);
                    await _db.SaveChangesAsync();

                    var emp = new Employee
                    {
                        UserId = user.UserId,
                        EmployeeNo = finalEmployeeNo,
                        JobTitle = model.JobTitle?.Trim(),
                        HireDate = DateOnly.FromDateTime(DateTime.Now),
                        Status = "Active",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _db.Employees.Add(emp);

                    var roleIdsToAssign = model.SelectedRoleIds != null
                        ? model.SelectedRoleIds.Distinct().ToList()
                        : new List<int>();

                    var memberRoleId = await _db.Roles
                        .Where(r => r.RoleName == MemberRoleName)
                        .Select(r => r.RoleId)
                        .FirstOrDefaultAsync();

                    if (memberRoleId != 0 && !roleIdsToAssign.Contains(memberRoleId))
                    {
                        roleIdsToAssign.Add(memberRoleId);
                    }

                    foreach (var roleId in roleIdsToAssign)
                    {
                        _db.UserRoles.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            AssignedBy = currentUserId,
                            AssignedAt = DateTime.Now
                        });
                    }

                    _db.AuditLogs.Add(new AuditLog
                    {
                        UserId = currentUserId,
                        Action = "CreateUser",
                        EntityType = "User",
                        EntityId = user.UserId.ToString(),
                        NewValue = $"Created employee: {user.Email} ({finalEmployeeNo})",
                        CreatedAt = DateTime.Now
                    });

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = $"成功建立員工帳號：{user.Name} ({user.Email})，員工編號：{finalEmployeeNo}";
                    return RedirectToAction(nameof(UserList));
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < maxRetries)
                {
                    // [1] 命中 EmployeeNo（或 Email）unique constraint 衝突，重新產生編號後重試
                    await transaction.RollbackAsync();
                    _logger.LogWarning(ex, "Unique constraint conflict on attempt {Attempt} while creating user, regenerating EmployeeNo", attempt);
                    finalEmployeeNo = await GenerateNextEmployeeNoAsync();
                    continue;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Failed to create user with Email {Email}", normalizedEmail); // [4]

                    ModelState.AddModelError("", "建立帳號過程發生錯誤，請稍後再試。");
                    model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                    model.EmployeeNo = finalEmployeeNo;
                    return View(model);
                }
            }

            // 重試多次仍失敗
            _logger.LogError("Failed to create user with Email {Email} after {MaxRetries} retries due to repeated unique constraint conflicts", normalizedEmail, maxRetries);
            ModelState.AddModelError("", "建立帳號過程發生錯誤（編號衝突），請稍後再試。");
            model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
            model.EmployeeNo = await GenerateNextEmployeeNoAsync();
            return View(model);
        }

        public async Task<IActionResult> EditUser(int id)
        {
            var userData = await (from e in _db.Employees
                                  join u in _db.Users on e.UserId equals u.UserId
                                  where u.UserId == id
                                  select new { User = u, Emp = e })
                                 .FirstOrDefaultAsync();

            if (userData == null)
            {
                TempData["ErrorMessage"] = "查無該員工資料。";
                return RedirectToAction(nameof(UserList));
            }

            var currentRoleIds = await _db.UserRoles
                .Where(ur => ur.UserId == id)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var model = new EditUserViewModel
            {
                UserId = userData.User.UserId,
                EmployeeNo = userData.Emp.EmployeeNo,
                Email = userData.User.Email,
                Name = userData.User.Name,
                Phone = userData.User.Phone,
                JobTitle = userData.Emp.JobTitle,
                Status = userData.Emp.Status,
                SelectedRoleIds = currentRoleIds,
                AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            // [6] Email 唯一性檢查改為不分大小寫
            var normalizedEmail = model.Email?.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.UserId != model.UserId))
            {
                ModelState.AddModelError("Email", "此 Email 已被其他帳號使用");
            }

            // [7] 禁止管理員把自己的在職狀態改為離職/留職停薪，避免自己被鎖在系統外
            if (model.UserId == currentUserId && !string.Equals(model.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "不可將自己的帳號狀態變更為離職或留職停薪。");
            }

            // [7] 禁止管理員把自己身上的 Admin 角色移除，避免自己被鎖在系統外
            if (model.UserId == currentUserId)
            {
                var adminRoleId = await _db.Roles
                    .Where(r => r.RoleName == AdminRoleName)
                    .Select(r => r.RoleId)
                    .FirstOrDefaultAsync();

                var currentlyHasAdmin = await _db.UserRoles
                    .AnyAsync(ur => ur.UserId == currentUserId && ur.RoleId == adminRoleId);

                var willKeepAdmin = adminRoleId != 0 && (model.SelectedRoleIds?.Contains(adminRoleId) ?? false);

                if (currentlyHasAdmin && !willKeepAdmin)
                {
                    ModelState.AddModelError("", "不可移除自己帳號的管理員（Admin）角色，以免無法再存取設定頁面。");
                }
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                return View(model);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var user = await _db.Users.FindAsync(model.UserId);
                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == model.UserId);

                if (user == null || emp == null)
                {
                    TempData["ErrorMessage"] = "找不到該員工資料。";
                    return RedirectToAction(nameof(UserList));
                }

                user.Name = model.Name?.Trim();
                user.Email = normalizedEmail;
                user.Phone = model.Phone?.Trim();
                user.UpdatedAt = DateTime.Now;

                emp.JobTitle = model.JobTitle?.Trim();
                emp.Status = model.Status;
                emp.UpdatedAt = DateTime.Now;

                var currentRoles = await _db.UserRoles
                    .Where(ur => ur.UserId == model.UserId)
                    .ToListAsync();

                var currentRoleIds = currentRoles.Select(ur => ur.RoleId).ToList();
                var targetRoleIds = model.SelectedRoleIds ?? new List<int>();

                var staffRoleIds = (await GetAvailableRolesAsync(excludeMemberRoles: true))
                    .Select(r => r.RoleId)
                    .ToList();

                var rolesToRemove = currentRoles
                    .Where(ur => staffRoleIds.Contains(ur.RoleId) && !targetRoleIds.Contains(ur.RoleId))
                    .ToList();

                _db.UserRoles.RemoveRange(rolesToRemove);

                var roleIdsToAdd = targetRoleIds
                    .Where(id => staffRoleIds.Contains(id))
                    .Except(currentRoleIds)
                    .ToList();

                foreach (var roleId in roleIdsToAdd)
                {
                    _db.UserRoles.Add(new UserRole
                    {
                        UserId = model.UserId,
                        RoleId = roleId,
                        AssignedBy = currentUserId,
                        AssignedAt = DateTime.Now
                    });
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "EditUser",
                    EntityType = "Employee",
                    EntityId = emp.EmployeeId.ToString(),
                    NewValue = $"Updated employee: {user.Name} ({user.Email}), JobTitle: {emp.JobTitle}, Status: {emp.Status}",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"已成功修改員工資料：{user.Name}";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to edit user {UserId}", model.UserId); // [4]
                ModelState.AddModelError("", "更新員工資料時發生錯誤，請稍後再試。");
                model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployeeStatus(int userId, string status)
        {
            int currentUserId = GetCurrentUserId();

            // [2] 白名單驗證 status 參數，避免寫入非預期的值
            if (string.IsNullOrWhiteSpace(status) || !AllowedEmployeeStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "無效的狀態值。";
                return RedirectToAction(nameof(UserList));
            }

            // [7] 禁止管理員把自己設為離職/留職停薪，避免自己被鎖在系統外
            if (userId == currentUserId && !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "不可將自己的帳號狀態變更為離職或留職停薪。";
                return RedirectToAction(nameof(UserList));
            }

            var user = await _db.Users.FindAsync(userId);
            var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);

            if (user == null || emp == null)
            {
                TempData["ErrorMessage"] = "找不到相關員工資料。";
                return RedirectToAction(nameof(UserList));
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                emp.Status = status;
                emp.UpdatedAt = DateTime.Now;

                if (status.Equals("Resigned", StringComparison.OrdinalIgnoreCase) ||
                    status.Equals("OnLeave", StringComparison.OrdinalIgnoreCase))
                {
                    var rolesToRemove = await (from ur in _db.UserRoles
                                               join r in _db.Roles on ur.RoleId equals r.RoleId
                                               where ur.UserId == userId && r.RoleName != MemberRoleName
                                               select ur).ToListAsync();
                    _db.UserRoles.RemoveRange(rolesToRemove);
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "UpdateEmployeeStatus",
                    EntityType = "Employee",
                    EntityId = emp.EmployeeId.ToString(),
                    NewValue = $"Updated employee {emp.EmployeeNo} status to '{status}'",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"已成功將 {user.Name} 的狀態變更為：{status}";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update employee status for UserId {UserId} to {Status}", userId, status); // [4]
                TempData["ErrorMessage"] = "狀態變更失敗，請稍後再試。";
                return RedirectToAction(nameof(UserList));
            }
        }

        #endregion

        #region Private Helpers

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        // [1] 判斷例外是否為 unique constraint 違反（SQL Server: 2601/2627，PostgreSQL: 23505）
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("2601") ||
                   message.Contains("2627") ||
                   message.Contains("23505") ||
                   message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> GenerateNextEmployeeNoAsync()
        {
            const string prefix = "EMP";
            var lastEmpNo = await _db.Employees
                .Where(e => e.EmployeeNo.StartsWith(prefix))
                .OrderByDescending(e => e.EmployeeNo.Length)
                .ThenByDescending(e => e.EmployeeNo)
                .Select(e => e.EmployeeNo)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(lastEmpNo)) return $"{prefix}0001";

            var numberPart = lastEmpNo.Substring(prefix.Length);
            if (int.TryParse(numberPart, out int currentNum))
            {
                return $"{prefix}{(currentNum + 1):D4}";
            }

            int count = await _db.Employees.CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        private async Task<List<RoleOptionDto>> GetAvailableRolesAsync(bool excludeMemberRoles = false)
        {
            var query = _db.Roles.Where(r => r.Status);

            if (excludeMemberRoles)
            {
                query = query.Where(r => r.RoleName != MemberRoleName);
            }

            return await query
                .Select(r => new RoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Status = r.Status
                }).ToListAsync();
        }

        private async Task<List<PermissionOptionDto>> GetAvailablePermissionsAsync()
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

        #endregion
    }
}