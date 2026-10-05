using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Services.Members
{
    public class EmployeeAccountService : IEmployeeAccountService
    {
        private readonly dbVenueContext _db;
        private readonly ILogger<EmployeeAccountService> _logger;

        // 全系統統一的時間來源（套用校時偏移量、已捨去毫秒以符合 datetime2(0)），
        // 不直接用 DateTime.Now，才能跟預約、評論等其他模組的時間一致。
        private readonly ITimeService _time;

        // [搬移自 SettingController] 分頁大小上下限
        private const int MinPageSize = 1;
        private const int MaxPageSize = 100;

        public EmployeeAccountService(dbVenueContext db, ILogger<EmployeeAccountService> logger, ITimeService time)
        {
            _db = db;
            _logger = logger;
            _time = time;
        }

        // ============================================================
        // 查詢類方法
        // ============================================================

        public async Task<UserListViewModel> GetUserListAsync(string? keyword, int? roleId, string? status, int page, int pageSize)
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

            return new UserListViewModel
            {
                Keyword = keyword,
                SelectedRoleId = roleId,
                SelectedStatus = status,
                SelectedUserType = "employee",
                AvailableRoles = availableRoles,
                Users = usersList,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
            };
        }

        public async Task<RegisterUserViewModel> GetEmployeeForCreateAsync()
        {
            return new RegisterUserViewModel
            {
                AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true),
                EmployeeNo = await GenerateNextEmployeeNoAsync(),
                Birth = DateOnly.FromDateTime(_time.Now)
            };
        }

        public async Task<EditUserViewModel?> GetEmployeeForEditAsync(int userId)
        {
            var userData = await (from e in _db.Employees
                                  join u in _db.Users on e.UserId equals u.UserId
                                  where u.UserId == userId
                                  select new { User = u, Emp = e })
                                 .FirstOrDefaultAsync();

            if (userData == null) return null;

            var currentRoleIds = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            // [方案B] 若此員工目前是離職/留停，查出上次被移除的角色快照，供畫面顯示參考
            bool isCurrentlyInactive = userData.Emp.Status.Equals(EmployeeStatuses.Resigned, StringComparison.OrdinalIgnoreCase)
                                     || userData.Emp.Status.Equals(EmployeeStatuses.OnLeave, StringComparison.OrdinalIgnoreCase);

            string? previousRoleNames = isCurrentlyInactive
                ? await GetLastRemovedRolesSnapshotAsync(userId)
                : null;

            return new EditUserViewModel
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
                PreviousRoleNames = previousRoleNames
            };
        }

        public async Task<List<RoleOptionDto>> GetAvailableRolesAsync(bool excludeMemberRoles = false)
        {
            var query = _db.Roles.Where(r => r.Status);

            if (excludeMemberRoles)
            {
                query = query.Where(r => r.RoleName != RoleNames.Member);
            }

            return await query
                .Select(r => new RoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Status = r.Status
                }).ToListAsync();
        }

        // ============================================================
        // 寫入類方法
        // ============================================================

        public async Task<ServiceResult> CreateEmployeeAsync(RegisterUserViewModel model, int currentUserId)
        {
            // [6] Email 唯一性檢查改為不分大小寫，避免 A@b.com / a@b.com 視為不同帳號
            var normalizedEmail = model.Email?.Trim().ToLowerInvariant();
            var errors = new List<(string Key, string Message)>();

            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                errors.Add(("Email", "請輸入 Email"));
            }
            else if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                errors.Add(("Email", "此 Email 已被註冊使用"));
            }

            // [補上] 職稱必填。RegisterUserViewModel 的註解說「移除 [Required]，改由 Controller 動態驗證」，
            // 但實際上沒有任何地方驗證；Employee.JobTitle 在實體上是非 null（資料庫欄位為 NOT NULL），
            // 留空會變成 INSERT NULL 失敗，使用者只看到泛用的「建立帳號過程發生錯誤」。
            // 編輯員工（EditUserViewModel）本來就是 [Required]，這裡跟它保持一致。
            if (string.IsNullOrWhiteSpace(model.JobTitle))
            {
                errors.Add((nameof(model.JobTitle), "請輸入職稱"));
            }

            // [補修正] 一個角色都不勾也能建立員工，會變成「建立成功、但永遠無法登入後台」的殭屍帳號
            // （系統只會自動補 Member，登入時 userRoles.Any() 為 false 而被擋下）。
            //
            // [再修正 1] 錯誤的 Key 用 string.Empty，不是 "SelectedRoleIds"：
            // CreateUser/EditUser 畫面用的是 asp-validation-summary="ModelOnly"，
            // 而且角色區塊沒有 asp-validation-for="SelectedRoleIds"，
            // 帶欄位 Key 的錯誤根本不會顯示，使用者按儲存只會看到畫面重新整理、沒有任何提示。
            //
            // [再修正 2] 只檢查「至少勾一個」不能保證是真正可用的後台角色：
            // 繞過畫面直接送出不存在 / 已停用 / Member 的 RoleId 也會通過，
            // 前兩者會在寫入 UserRoles 時撞 FK 變成泛用錯誤。
            // 這裡跟 UpdateEmployeeAsync 一致，只接受「啟用中、且不是 Member」的角色。
            var selectedRoleIds = model.SelectedRoleIds?.Distinct().ToList() ?? new List<int>();
            if (!selectedRoleIds.Any())
            {
                errors.Add((string.Empty, "請至少選擇一個後台角色，否則建立的帳號將無法登入後台。"));
            }
            else
            {
                var validStaffRoleIds = (await GetAvailableRolesAsync(excludeMemberRoles: true))
                    .Select(r => r.RoleId)
                    .ToHashSet();

                if (selectedRoleIds.Any(id => !validStaffRoleIds.Contains(id)))
                {
                    errors.Add((string.Empty, "選擇的角色中包含不存在、已停用或不可指派的角色。"));
                }
            }

            // Trim 之後再比對與寫入：前後多一個空白就能繞過重複檢查，最後才在資料庫撞唯一索引
            string finalEmployeeNo = model.EmployeeNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(finalEmployeeNo) || await _db.Employees.AnyAsync(e => e.EmployeeNo == finalEmployeeNo))
            {
                finalEmployeeNo = await GenerateNextEmployeeNoAsync();
            }

            // 不論接下來驗證過不過，都先把目前打算使用的編號寫回 model，
            // 失敗時 Controller 把同一個 model 拿去 View()，畫面上看到的編號就是最新的
            model.EmployeeNo = finalEmployeeNo;

            if (errors.Any())
            {
                return ServiceResult.ValidationFailed(errors);
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
                        Email = normalizedEmail!,
                        PasswordHash = PasswordHelper.HashPassword(model.Password),
                        Name = model.Name.Trim(),
                        Phone = model.Phone?.Trim(),
                        Status = UserStatuses.Active,
                        Birth = model.Birth,
                        CumulativeConsumption = 0,
                        CumulativeVisitTime = 0,
                        FailedLoginCount = 0,
                        NoShowCount = 0,
                        CreatedAt = _time.Now,
                        UpdatedAt = _time.Now
                    };

                    _db.Users.Add(user);
                    await _db.SaveChangesAsync();

                    var emp = new Employee
                    {
                        UserId = user.UserId,
                        EmployeeNo = finalEmployeeNo,
                        JobTitle = model.JobTitle!.Trim(), // 上面已驗證非空白
                        HireDate = DateOnly.FromDateTime(_time.Now),
                        Status = EmployeeStatuses.Active,
                        CreatedAt = _time.Now,
                        UpdatedAt = _time.Now
                    };
                    _db.Employees.Add(emp);

                    var roleIdsToAssign = model.SelectedRoleIds != null
                        ? model.SelectedRoleIds.Distinct().ToList()
                        : new List<int>();

                    var memberRoleId = await _db.Roles
                        .Where(r => r.RoleName == RoleNames.Member)
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
                            AssignedAt = _time.Now
                        });
                    }

                    _db.AuditLogs.Add(new AuditLog
                    {
                        UserId = currentUserId,
                        Action = AuditActions.CreateUser,
                        EntityType = AuditEntityTypes.User,
                        EntityId = user.UserId.ToString(),
                        NewValue = $"Created employee: {user.Email} ({finalEmployeeNo})",
                        CreatedAt = _time.Now
                    });

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult.Success();
                }
                catch (DbUpdateException ex) when (attempt < maxRetries)
                {
                    await transaction.RollbackAsync();

                    // [BUG 修正] SaveChanges 失敗後，這一輪 Add 進去的 User / Employee / UserRole / AuditLog
                    // 仍留在 ChangeTracker 裡（狀態還是 Added）。不清掉的話，下一輪重試的 SaveChanges
                    // 會連同這些舊物件一起再送出，一樣撞上同一個唯一索引，重試永遠不會成功。
                    _db.ChangeTracker.Clear();

                    var conflictField = GetUniqueConstraintConflictField(ex);

                    if (conflictField == ConflictField.EmployeeNo)
                    {
                        // [FIX] 確定是 EmployeeNo 撞號，重新產生編號後重試
                        _logger.LogWarning(ex, "EmployeeNo conflict on attempt {Attempt}, regenerating", attempt);
                        finalEmployeeNo = await GenerateNextEmployeeNoAsync();
                        model.EmployeeNo = finalEmployeeNo;
                        continue;
                    }

                    if (conflictField == ConflictField.Email)
                    {
                        // [FIX] 是 Email 撞號（極端併發情境），直接回錯誤，不做無意義的重試
                        _logger.LogWarning(ex, "Email conflict on attempt {Attempt} for {Email}", attempt, normalizedEmail);
                        return ServiceResult.ValidationFailed("Email", "此 Email 已被註冊使用");
                    }

                    _logger.LogError(ex, "Unrecognized unique constraint conflict while creating user {Email}", normalizedEmail);
                    return ServiceResult.Error("建立帳號過程發生錯誤，請稍後再試。");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _db.ChangeTracker.Clear(); // 原因同上
                    _logger.LogError(ex, "Failed to create user with Email {Email}", normalizedEmail);
                    return ServiceResult.Error("建立帳號過程發生錯誤，請稍後再試。");
                }
            }

            // 重試多次仍失敗
            _logger.LogError("Failed to create user with Email {Email} after {MaxRetries} retries due to repeated EmployeeNo conflicts", normalizedEmail, maxRetries);
            model.EmployeeNo = await GenerateNextEmployeeNoAsync();
            return ServiceResult.Error("建立帳號過程發生錯誤（編號衝突），請稍後再試。");
        }

        public async Task<ServiceResult> UpdateEmployeeAsync(EditUserViewModel model, int currentUserId)
        {
            // [6] Email 唯一性檢查改為不分大小寫
            var normalizedEmail = model.Email?.Trim().ToLowerInvariant();
            var errors = new List<(string Key, string Message)>();

            if (await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail && u.UserId != model.UserId))
            {
                errors.Add(("Email", "此 Email 已被其他帳號使用"));
            }

            // [補修正] 原本沒有驗證 model.Status 是否為合法值，直接寫入 Employees.Status，
            // 繞過前端畫面就能寫入任意字串。現在用 EmployeeStatuses.AllowedStatuses 白名單擋下。
            //
            // [再修正] 白名單是「不分大小寫」比對，但寫進資料庫的卻是使用者送來的原字串。
            // 送 "active" 會通過驗證、存成 "active"；而登入（AuthenticationService）與
            // EmployeeAuthorizeFilter 是用 C# 的 != "Active" 比對（區分大小寫），
            // 這位員工之後會被判定為「狀態異常」而無法登入。
            // 所以這裡取出白名單裡的標準拼法，之後一律寫入標準拼法。
            var canonicalStatus = EmployeeStatuses.AllowedStatuses
                .FirstOrDefault(s => string.Equals(s, model.Status, StringComparison.OrdinalIgnoreCase));
            if (canonicalStatus == null)
            {
                errors.Add(("Status", "無效的員工狀態值。"));
            }

            // [7] 禁止管理員把自己的在職狀態改為離職/留職停薪，避免自己被鎖在系統外
            if (model.UserId == currentUserId && !string.Equals(model.Status, EmployeeStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(("", "不可將自己的帳號狀態變更為離職或留職停薪。"));
            }

            // [7] 禁止管理員把自己身上的 Admin 角色移除，避免自己被鎖在系統外
            if (model.UserId == currentUserId)
            {
                var adminRoleId = await _db.Roles
                    .Where(r => r.RoleName == RoleNames.Admin)
                    .Select(r => r.RoleId)
                    .FirstOrDefaultAsync();

                var currentlyHasAdmin = await _db.UserRoles
                    .AnyAsync(ur => ur.UserId == currentUserId && ur.RoleId == adminRoleId);

                var willKeepAdmin = adminRoleId != 0 && (model.SelectedRoleIds?.Contains(adminRoleId) ?? false);

                if (currentlyHasAdmin && !willKeepAdmin)
                {
                    errors.Add(("", "不可移除自己帳號的管理員（Admin）角色，以免無法再存取設定頁面。"));
                }
            }

            // [補修正] 原本沒有這個檢查，管理員編輯「別人」的帳號時，
            // 可以把對方所有角色全部取消勾選後儲存，對方會瞬間失去登入後台的能力，
            // 而且系統完全不會警告。這裡提前查出可用角色清單（GetAvailableRolesAsync(excludeMemberRoles: true)），
            // 算出「這次異動完成後，對方實際會剩下幾個後台角色」，一個都不剩就擋下來。
            // 注意：這跟上面「不可移除自己的 Admin 角色」是兩條獨立規則——
            // 上面那條只管「自己」且只管「Admin 角色」，這條不分對象、只要結果是 0 個角色就擋，
            // 兩條檢查都要留著，不能互相取代。
            var staffRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
            var staffRoleIds = staffRoles.Select(r => r.RoleId).ToList();
            var targetRoleIds = model.SelectedRoleIds ?? new List<int>();
            var targetStaffRoleIds = targetRoleIds.Where(id => staffRoleIds.Contains(id)).ToList();

            // [修正-回歸問題] 這條規則只在「對方狀態還是 Active（預期還能登入後台）」時才檢查。
            // 畫面上把狀態切成 Resigned/OnLeave 時，前端 JS 會主動把所有角色 checkbox 取消勾選，
            // 這是刻意的設計（離職就是要清空後台角色），不該被這條規則擋下來，
            // 否則離職/留停功能會整個無法儲存。
            bool expectedToStayActive = string.Equals(model.Status, EmployeeStatuses.Active, StringComparison.OrdinalIgnoreCase);
            if (expectedToStayActive && !targetStaffRoleIds.Any())
            {
                // Key 用 string.Empty：畫面是 ModelOnly 摘要，帶欄位 Key 的錯誤不會顯示（原因見 CreateEmployeeAsync）
                errors.Add((string.Empty, "請至少保留一個後台角色，否則此帳號將無法登入後台。"));
            }

            if (errors.Any())
            {
                return ServiceResult.ValidationFailed(errors);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var user = await _db.Users.FindAsync(model.UserId);
                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == model.UserId);

                if (user == null || emp == null)
                {
                    // 驗證都通過了，但真正寫入時卻發現資料不見了（競態情況）。
                    // 刻意跟「網址打錯」的 404 區分開，交給 Controller 用 TempData + 導回清單頁處理。
                    await transaction.RollbackAsync();
                    return ServiceResult.NotFound();
                }

                user.Name = model.Name?.Trim();
                user.Email = normalizedEmail;
                user.Phone = model.Phone?.Trim();
                user.UpdatedAt = _time.Now;

                emp.JobTitle = model.JobTitle?.Trim();
                emp.Status = canonicalStatus!; // 通過驗證才會走到這裡，必定非 null
                emp.UpdatedAt = _time.Now;

                var currentRoles = await _db.UserRoles
                    .Where(ur => ur.UserId == model.UserId)
                    .ToListAsync();

                var currentRoleIds = currentRoles.Select(ur => ur.RoleId).ToList();
                // staffRoles / staffRoleIds / targetRoleIds 沿用上面驗證階段已經算好的結果，不重新查詢

                var rolesToRemove = currentRoles
                    .Where(ur => staffRoleIds.Contains(ur.RoleId) && !targetRoleIds.Contains(ur.RoleId))
                    .ToList();

                // [方案B] 離職/留停前，先記錄即將被移除的角色名稱清單，供日後復職時人工查閱。
                // [小優化] 角色名稱直接從上面已經查過的 staffRoles 裡取，不用再多查一次資料庫。
                string? removedRolesSnapshot = null;
                if (rolesToRemove.Any())
                {
                    var removedRoleIds = rolesToRemove.Select(ur => ur.RoleId).ToHashSet();
                    var removedRoleNames = staffRoles
                        .Where(r => removedRoleIds.Contains(r.RoleId))
                        .Select(r => r.RoleName)
                        .ToList();
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
                        AssignedAt = _time.Now
                    });
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = AuditActions.EditUser,
                    EntityType = AuditEntityTypes.Employee,
                    EntityId = emp.EmployeeId.ToString(),
                    OldValue = removedRolesSnapshot,
                    NewValue = $"Updated employee: {user.Name} ({user.Email}), JobTitle: {emp.JobTitle}, Status: {emp.Status}",
                    CreatedAt = _time.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to edit user {UserId}", model.UserId);
                return ServiceResult.Error("更新員工資料時發生錯誤，請稍後再試。");
            }
        }

        // ============================================================
        // 會員升格為員工
        // ============================================================

        // 舊系統遺留的角色名稱，原本 MemberController 的角色清單會把它排除，這裡維持一致
        private const string LegacyCustomerRoleName = "Customer";

        public async Task<ServiceResult<ConvertEmployeeViewModel>> GetConvertToEmployeeFormAsync(int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return ServiceResult<ConvertEmployeeViewModel>.NotFound();

            if (await _db.Employees.AnyAsync(e => e.UserId == userId))
            {
                return ServiceResult<ConvertEmployeeViewModel>.ValidationFailed(string.Empty, "該使用者已經是員工。");
            }

            return ServiceResult<ConvertEmployeeViewModel>.Success(new ConvertEmployeeViewModel
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                EmployeeNo = await GenerateNextEmployeeNoAsync(),
                AvailableRoles = await GetConvertibleRolesAsync()
            });
        }

        public async Task<List<RoleOptionDto>> GetConvertibleRolesAsync()
        {
            var roles = await GetAvailableRolesAsync(excludeMemberRoles: true);
            return roles.Where(r => r.RoleName != LegacyCustomerRoleName).ToList();
        }

        public async Task<ServiceResult<string>> ConvertMemberToEmployeeAsync(ConvertEmployeeViewModel model, int currentUserId)
        {
            // [補修正] 原本「已經是員工」只在 GET 檢查，POST 沒檢查，
            // 重複送出時會撞 UQ_Employees_UserId，使用者只看到泛用的錯誤訊息。
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == model.UserId);
            if (user == null) return ServiceResult<string>.NotFound();

            if (await _db.Employees.AnyAsync(e => e.UserId == user.UserId))
            {
                return ServiceResult<string>.ValidationFailed(string.Empty, "該使用者已經是員工。");
            }

            var errors = new List<(string Key, string Message)>();

            // [補修正] 先 Trim 再檢查重複：原本檢查的是未 Trim 的值、寫入的卻是 Trim 後的值，
            // 前後多一個空白就能繞過檢查，最後才在資料庫撞唯一索引。
            var employeeNo = model.EmployeeNo?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(employeeNo))
            {
                errors.Add((nameof(model.EmployeeNo), "請輸入員工編號"));
            }
            else if (await _db.Employees.AnyAsync(e => e.EmployeeNo == employeeNo))
            {
                errors.Add((nameof(model.EmployeeNo), "此員工編號已存在"));
            }

            // 職稱必填，原因同 CreateEmployeeAsync（Employee.JobTitle 為非 null 欄位）
            if (string.IsNullOrWhiteSpace(model.JobTitle))
            {
                errors.Add((nameof(model.JobTitle), "請輸入職稱"));
            }

            // 角色驗證：Key 用 string.Empty，因為畫面是 ModelOnly 摘要、角色區塊沒有欄位級的錯誤顯示
            // （同 CreateEmployeeAsync）。原本完全沒驗證角色 ID，也允許一個角色都不選。
            var selectedRoleIds = model.SelectedRoleIds?.Distinct().ToList() ?? new List<int>();
            if (!selectedRoleIds.Any())
            {
                errors.Add((string.Empty, "請至少選擇一個後台角色，否則此員工將無法登入後台。"));
            }
            else
            {
                var validRoleIds = (await GetConvertibleRolesAsync()).Select(r => r.RoleId).ToHashSet();
                if (selectedRoleIds.Any(id => !validRoleIds.Contains(id)))
                {
                    errors.Add((string.Empty, "選擇的角色中包含不存在、已停用或不可指派的角色。"));
                }
            }

            if (errors.Any())
            {
                return ServiceResult<string>.ValidationFailed(errors);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                _db.Employees.Add(new Employee
                {
                    UserId = user.UserId,
                    EmployeeNo = employeeNo,
                    JobTitle = model.JobTitle!.Trim(), // 上面已驗證非空白
                    HireDate = DateOnly.FromDateTime(_time.Now),
                    Status = EmployeeStatuses.Active,
                    CreatedAt = _time.Now,
                    UpdatedAt = _time.Now
                });

                // 這個人原本就有的角色（例如 Member）不重複指派
                var existingRoleIds = await _db.UserRoles
                    .Where(ur => ur.UserId == user.UserId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                foreach (var roleId in selectedRoleIds.Except(existingRoleIds))
                {
                    _db.UserRoles.Add(new UserRole
                    {
                        UserId = user.UserId,
                        RoleId = roleId,
                        AssignedBy = currentUserId,
                        AssignedAt = _time.Now
                    });
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = AuditActions.ConvertToEmployee,
                    EntityType = AuditEntityTypes.User,
                    EntityId = user.UserId.ToString(),
                    NewValue = $"Converted member {user.Email} to employee ({employeeNo})",
                    CreatedAt = _time.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return ServiceResult<string>.Success(user.Name);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();

                // 兩個人同時用同一個編號升格時，應用層檢查擋不住，由資料庫唯一索引兜底
                if (GetUniqueConstraintConflictField(ex) == ConflictField.EmployeeNo)
                {
                    _logger.LogWarning(ex, "EmployeeNo conflict while converting UserId {UserId}", model.UserId);
                    return ServiceResult<string>.ValidationFailed(nameof(model.EmployeeNo), "此員工編號已存在");
                }

                _logger.LogError(ex, "Failed to convert UserId {UserId} to employee", model.UserId);
                return ServiceResult<string>.Error("轉變員工過程發生錯誤。");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Failed to convert UserId {UserId} to employee", model.UserId);
                return ServiceResult<string>.Error("轉變員工過程發生錯誤。");
            }
        }

        // ============================================================
        // Private Helpers
        // ============================================================

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

        // [已知效能議題，這次重構先不動] .Length 排序無法使用索引，
        // 資料量變大後這支查詢會變慢，之後有空再評估是否要改用數字欄位排序。
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

        // [方案B] 查詢此員工「最近一次」被移除角色時的快照
        private async Task<string?> GetLastRemovedRolesSnapshotAsync(int userId)
        {
            var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
            if (emp == null) return null;

            var employeeIdStr = emp.EmployeeId.ToString();

            var lastLog = await _db.AuditLogs
                .Where(a => a.EntityType == AuditEntityTypes.Employee
                            && (a.Action == AuditActions.UpdateEmployeeStatus || a.Action == AuditActions.EditUser)
                            && a.EntityId == employeeIdStr
                            && a.OldValue != null)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            return lastLog?.OldValue;
        }
    }
}