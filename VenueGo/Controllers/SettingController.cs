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

        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _db.Roles.FindAsync(id);
            if (role == null) return NotFound();

            var selectedPermIds = await _db.RolePermissions
                .Where(rp => rp.RoleId == id && rp.Status)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            // [NEW] 是否已有使用者使用此角色，用於畫面上提示「名稱不可修改」
            var isRoleInUse = await IsRoleInUseAsync(id);

            var model = new RoleEditViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description,
                Status = role.Status,
                SelectedPermissionIds = selectedPermIds,
                AvailablePermissions = await GetAvailablePermissionsAsync()
                // 若 RoleEditViewModel 有對應欄位（例如 IsInUse），可在此一併帶入畫面顯示提示文字：
                // IsInUse = isRoleInUse
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
            var trimmedName = model.RoleName?.Trim() ?? string.Empty;
            bool nameChanged = !string.Equals(role.RoleName, trimmedName, StringComparison.Ordinal);

            // [5] 禁止把系統保留角色改名，避免依賴角色名稱的邏輯悄悄失效
            if (isReservedRole && nameChanged)
            {
                ModelState.AddModelError("RoleName", $"系統角色「{role.RoleName}」的名稱不可修改。");
            }

            // [5] 禁止把其他角色改名成與保留角色相同的名稱
            if (!isReservedRole && ReservedRoleNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("RoleName", "此名稱為系統保留角色名稱，請使用其他名稱。");
            }

            // [NEW] 應用層檢查角色名稱是否與其他既有角色重複（排除自己）
            // 注意：此檢查未搭配資料庫 unique index，高併發下仍有極小機率發生 race condition，
            // 但已足以擋下一般情境下的重複命名。
            if (!isReservedRole && nameChanged
                && await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: model.RoleId))
            {
                ModelState.AddModelError("RoleName", "角色名稱已存在，請使用其他名稱。");
            }

            // [NEW] 若此角色已有使用者使用，禁止修改角色名稱（權限仍可調整）
            if (!isReservedRole && nameChanged && await IsRoleInUseAsync(model.RoleId))
            {
                ModelState.AddModelError("RoleName", "此角色已有使用者使用，無法修改角色名稱。若需調整權限，請保留原名稱後再送出。");
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

            role.RoleName = trimmedName;
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
                NewValue = $"Updated Role: {role.RoleName}",
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

        [HttpGet]
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
            var trimmedName = model.RoleName?.Trim() ?? string.Empty;

            // [FIX] 用 trim 後的名稱做重複性檢查，避免「Staff 」通過檢查但存入後變成重複
            if (await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: null))
            {
                ModelState.AddModelError("RoleName", "角色名稱已存在");
            }

            // [5] 新建角色也不可佔用系統保留名稱
            if (ReservedRoleNames.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
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
                // [NEW] 交易內再次確認名稱未被搶先建立（縮小應用層檢查與寫入之間的競態窗口）
                if (await IsRoleNameDuplicateAsync(trimmedName, excludeRoleId: null))
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError("RoleName", "角色名稱已存在，請使用其他名稱。");
                    model.AvailablePermissions = await GetAvailablePermissionsAsync();
                    return View(model);
                }

                var role = new Role
                {
                    RoleName = trimmedName,
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
                _logger.LogError(ex, "Failed to create role {RoleName}", trimmedName); // [4]
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
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                ModelState.AddModelError("Email", "請輸入 Email");
            }
            else if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
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
                catch (DbUpdateException ex) when (attempt < maxRetries)
                {
                    await transaction.RollbackAsync();

                    var conflictField = GetUniqueConstraintConflictField(ex);

                    if (conflictField == ConflictField.EmployeeNo)
                    {
                        // [FIX] 確定是 EmployeeNo 撞號，重新產生編號後重試
                        _logger.LogWarning(ex, "EmployeeNo conflict on attempt {Attempt}, regenerating", attempt);
                        finalEmployeeNo = await GenerateNextEmployeeNoAsync();
                        continue;
                    }

                    if (conflictField == ConflictField.Email)
                    {
                        // [FIX] 是 Email 撞號（極端併發情境），直接回錯誤，不做無意義的重試
                        _logger.LogWarning(ex, "Email conflict on attempt {Attempt} for {Email}", attempt, normalizedEmail);
                        ModelState.AddModelError("Email", "此 Email 已被註冊使用");
                        model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                        model.EmployeeNo = finalEmployeeNo;
                        return View(model);
                    }

                    // 無法判斷具體衝突欄位，視為一般錯誤，不再重試
                    _logger.LogError(ex, "Unrecognized unique constraint conflict while creating user {Email}", normalizedEmail);
                    ModelState.AddModelError("", "建立帳號過程發生錯誤，請稍後再試。");
                    model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                    model.EmployeeNo = finalEmployeeNo;
                    return View(model);
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
            _logger.LogError("Failed to create user with Email {Email} after {MaxRetries} retries due to repeated EmployeeNo conflicts", normalizedEmail, maxRetries);
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

            // [NEW-方案B] 若此員工目前是離職/留停，查出上次被移除的角色快照，供畫面顯示參考
            bool isCurrentlyInactive = userData.Emp.Status.Equals("Resigned", StringComparison.OrdinalIgnoreCase)
                                     || userData.Emp.Status.Equals("OnLeave", StringComparison.OrdinalIgnoreCase);

            string? previousRoleNames = isCurrentlyInactive
                ? await GetLastRemovedRolesSnapshotAsync(id)
                : null;

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
                AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true),
                // [NEW-方案B][需確認] EditUserViewModel 需新增 public string? PreviousRoleNames { get; set; }
                // 若尚未新增此欄位，這行會編譯失敗，請先在 ViewModel 補上對應屬性，
                // 或先移除這行、改用 ViewBag/ViewData["PreviousRoleNames"] 暫時傳遞給 View。
                PreviousRoleNames = previousRoleNames
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

                // [NEW-方案B] 補上跟 UpdateEmployeeStatus 一致的角色快照記錄。
                // 原因：員工狀態也可能透過這個 EditUser 表單直接改成 Resigned/OnLeave，
                // 若只在 UpdateEmployeeStatus 記錄快照，會漏掉從這裡異動角色的情境。
                string? removedRolesSnapshot = null;
                if (rolesToRemove.Any())
                {
                    var removedRoleIds = rolesToRemove.Select(ur => ur.RoleId).ToList();
                    var removedRoleNames = await _db.Roles
                        .Where(r => removedRoleIds.Contains(r.RoleId))
                        .Select(r => r.RoleName)
                        .ToListAsync();

                    removedRolesSnapshot = string.Join(",", removedRoleNames);
                }

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
                    OldValue = removedRolesSnapshot, // [NEW-方案B] 此次異動被移除的角色快照（null 表示沒有角色被移除）
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

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> UpdateEmployeeStatus(int userId, string status)
        //{
        //    int currentUserId = GetCurrentUserId();

        //    // [2] 白名單驗證 status 參數，避免寫入非預期的值
        //    if (string.IsNullOrWhiteSpace(status) || !AllowedEmployeeStatuses.Contains(status))
        //    {
        //        TempData["ErrorMessage"] = "無效的狀態值。";
        //        return RedirectToAction(nameof(UserList));
        //    }

        //    // [7] 禁止管理員把自己設為離職/留職停薪，避免自己被鎖在系統外
        //    if (userId == currentUserId && !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        //    {
        //        TempData["ErrorMessage"] = "不可將自己的帳號狀態變更為離職或留職停薪。";
        //        return RedirectToAction(nameof(UserList));
        //    }

        //    var user = await _db.Users.FindAsync(userId);
        //    var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);

        //    if (user == null || emp == null)
        //    {
        //        TempData["ErrorMessage"] = "找不到相關員工資料。";
        //        return RedirectToAction(nameof(UserList));
        //    }

        //    using var transaction = await _db.Database.BeginTransactionAsync();

        //    try
        //    {
        //        emp.Status = status;
        //        emp.UpdatedAt = DateTime.Now;

        //        // [NEW-方案B] 離職/留停前，先記錄即將被移除的角色名稱清單，
        //        // 存進 AuditLog.OldValue，供日後復職時人工查閱、手動重新指派。
        //        // 注意：這裡假設 AuditLog Entity 已有 OldValue（string?）欄位；
        //        // 若沒有，請改成把角色清單併入 NewValue 文字內，或先新增該欄位。
        //        string? removedRolesSnapshot = null;

        //        if (status.Equals("Resigned", StringComparison.OrdinalIgnoreCase) ||
        //            status.Equals("OnLeave", StringComparison.OrdinalIgnoreCase))
        //        {
        //            var rolesToRemove = await (from ur in _db.UserRoles
        //                                       join r in _db.Roles on ur.RoleId equals r.RoleId
        //                                       where ur.UserId == userId && r.RoleName != MemberRoleName
        //                                       select new { ur, r.RoleName }).ToListAsync();

        //            if (rolesToRemove.Any())
        //            {
        //                // 用逗號分隔的角色名稱字串，方便之後直接顯示或簡單解析（不建議做複雜結構化解析，僅供人工參考）
        //                removedRolesSnapshot = string.Join(",", rolesToRemove.Select(x => x.RoleName));
        //            }

        //            _db.UserRoles.RemoveRange(rolesToRemove.Select(x => x.ur));
        //        }

        //        _db.AuditLogs.Add(new AuditLog
        //        {
        //            UserId = currentUserId,
        //            Action = "UpdateEmployeeStatus",
        //            EntityType = "Employee",
        //            EntityId = emp.EmployeeId.ToString(),
        //            OldValue = removedRolesSnapshot, // [NEW-方案B] 離職前的角色快照（null 表示無角色被移除，例如復職時 status=Active）
        //            NewValue = $"Updated employee {emp.EmployeeNo} status to '{status}'",
        //            CreatedAt = DateTime.Now
        //        });

        //        await _db.SaveChangesAsync();
        //        await transaction.CommitAsync();

        //        TempData["SuccessMessage"] = $"已成功將 {user.Name} 的狀態變更為：{status}";
        //        return RedirectToAction(nameof(UserList));
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        _logger.LogError(ex, "Failed to update employee status for UserId {UserId} to {Status}", userId, status); // [4]
        //        TempData["ErrorMessage"] = "狀態變更失敗，請稍後再試。";
        //        return RedirectToAction(nameof(UserList));
        //    }
        //}

        #endregion

        #region Private Helpers

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out int userId) || userId <= 0)
            {
                // [FIX] 無法解析出合法 UserId 時直接拋例外，
                // 避免 fallback 成 0 導致「自我鎖定保護」等安全檢查被意外繞過，
                // 也避免寫入錯誤的 AuditLog.UserId = 0
                _logger.LogError("Unable to resolve current user id from claims. Claim value: {ClaimValue}", userIdClaim);
                throw new InvalidOperationException("無法取得目前登入使用者的識別碼。");
            }

            return userId;
        }

        // [NEW] 應用層檢查角色名稱是否重複（大小寫不分，排除自己）
        // 說明：Roles.RoleName 目前資料庫層未加 unique index，
        // 此檢查與寫入之間仍存在極小的競態窗口（TOCTOU），
        // 若未來併發建立/改名角色的情境變多，建議評估補上資料庫 unique index。
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

        // [NEW] 檢查此角色目前是否已被任何使用者使用（有 UserRoles 紀錄）
        private async Task<bool> IsRoleInUseAsync(int roleId)
        {
            return await _db.UserRoles.AnyAsync(ur => ur.RoleId == roleId);
        }

        // [NEW-方案B] 查詢此員工「最近一次」被移除角色時的快照，
        // 供 EditUser 頁面顯示給管理員參考，方便復職時手動重新勾選角色。
        // 注意：角色移除可能發生在 UpdateEmployeeStatus 或 EditUser 這兩個 Action，
        // 因此這裡不限定 Action 名稱，只要 EntityType/EntityId 對得上且 OldValue 有值即可。
        // 回傳 null 表示查無紀錄（例如從未離職過，或最近一次異動沒有角色被移除）。
        private async Task<string?> GetLastRemovedRolesSnapshotAsync(int userId)
        {
            // [注意] AuditLog.EntityId 存的是 EmployeeId（字串），而不是 UserId，
            // 這裡需要先找出對應的 EmployeeId 再比對，避免抓錯人的紀錄。
            var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
            if (emp == null) return null;

            var employeeIdStr = emp.EmployeeId.ToString();

            var lastLog = await _db.AuditLogs
                .Where(a => a.EntityType == "Employee"
                            && (a.Action == "UpdateEmployeeStatus" || a.Action == "EditUser")
                            && a.EntityId == employeeIdStr
                            && a.OldValue != null)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            return lastLog?.OldValue;
        }

        private enum ConflictField { Unknown, EmployeeNo, Email }

        // [FIX] 解析例外訊息，判斷實際撞到哪個欄位的唯一索引
        // 依賴 SQL Server 錯誤訊息中會帶出索引名稱，例如：
        // "Cannot insert duplicate key row ... with unique index 'IX_Employees_EmployeeNo'."
        // 請確認資料庫中 EmployeeNo / Email 的唯一索引已依此命名，否則請調整下方比對字串。
        private static ConflictField GetUniqueConstraintConflictField(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;

            bool isUniqueViolation =
                message.Contains("2601") ||
                message.Contains("2627") ||
                message.Contains("23505") ||
                message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);

            if (!isUniqueViolation) return ConflictField.Unknown;

            if (message.Contains("EmployeeNo", StringComparison.OrdinalIgnoreCase))
                return ConflictField.EmployeeNo;

            if (message.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase))
                return ConflictField.Email;

            return ConflictField.Unknown;
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