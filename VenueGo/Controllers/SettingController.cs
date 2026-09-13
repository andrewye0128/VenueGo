using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.ViewModels;

namespace VenueGo.Controllers
{
    public class SettingController : Controller
    {
        private readonly dbVenueContext _db;

        public SettingController(dbVenueContext db)
        {
            _db = db;
        }

        // 預設首頁：直接導向角色權限管理
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Roles));
        }

        // 1. 角色權限列表頁面 (已優化：解決 N+1 查詢)
        public async Task<IActionResult> Roles()
        {
            var roles = await _db.Roles.AsNoTracking().ToListAsync();
            var roleIds = roles.Select(r => r.RoleId).ToList();

            // 批次取得各角色的使用者數量
            var userCounts = await _db.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .GroupBy(ur => ur.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count);

            // 批次取得各角色的有效權限名稱
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

        // 2. 編輯角色權限 (GET)
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _db.Roles.FindAsync(id);
            if (role == null) return NotFound();

            var selectedPermIds = await _db.RolePermissions
                .Where(rp => rp.RoleId == id && rp.Status)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var allPermissions = await GetAvailablePermissionsAsync();

            var model = new RoleEditViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                Description = role.Description,
                Status = role.Status,
                SelectedPermissionIds = selectedPermIds,
                AvailablePermissions = allPermissions
            };

            return View(model);
        }

        // 3. 儲存角色權限設定 (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(RoleEditViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }

            var role = await _db.Roles.FindAsync(model.RoleId);
            if (role == null) return NotFound();

            role.RoleName = model.RoleName;
            role.Description = model.Description;
            role.Status = model.Status;
            role.UpdatedAt = DateTime.Now;

            // 處理 RolePermission 關聯表
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

            // 紀錄操作日誌
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "UpdateRolePermissions",
                EntityType = "Role",
                EntityId = model.RoleId.ToString(),
                NewValue = $"Updated Role: {model.RoleName}",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "角色權限已成功更新！";

            return RedirectToAction(nameof(Roles));
        }

        // GET: Setting/CreateRole
        public async Task<IActionResult> CreateRole()
        {
            var model = new RoleCreateViewModel
            {
                AvailablePermissions = await GetAvailablePermissionsAsync()
            };

            return View(model);
        }

        // POST: Setting/CreateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(RoleCreateViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (await _db.Roles.AnyAsync(r => r.RoleName == model.RoleName))
            {
                ModelState.AddModelError("RoleName", "角色名稱已存在");
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
                await _db.SaveChangesAsync(); // 取得 auto-increment 的 RoleId

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
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "新增角色失敗，請稍後再試。");
                model.AvailablePermissions = await GetAvailablePermissionsAsync();
                return View(model);
            }
        }

        // GET: Setting/CreateUser
        public async Task<IActionResult> CreateUser()
        {
            var model = new RegisterUserViewModel
            {
                AvailableRoles = await GetAvailableRolesAsync(),
                EmployeeNo = await GenerateNextEmployeeNoAsync()
            };

            return View(model);
        }

        // POST: Setting/CreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterUserViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "此 Email 已被註冊使用");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await GetAvailableRolesAsync();
                return View(model);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var user = new User
                {
                    Email = model.Email,
                    PasswordHash = PasswordHelper.HashPassword(model.Password),
                    Name = model.Name,
                    Phone = model.Phone,
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

                if (!string.IsNullOrEmpty(model.EmployeeNo))
                {
                    var emp = new Employee
                    {
                        UserId = user.UserId,
                        EmployeeNo = model.EmployeeNo,
                        JobTitle = model.JobTitle?.Trim(),
                        HireDate = DateOnly.FromDateTime(DateTime.Now),
                        Status = "Active",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _db.Employees.Add(emp);
                }

                if (model.SelectedRoleIds != null && model.SelectedRoleIds.Any())
                {
                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        _db.UserRoles.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            AssignedBy = currentUserId,
                            AssignedAt = DateTime.Now
                        });
                    }
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "CreateUser",
                    EntityType = "User",
                    EntityId = user.UserId.ToString(),
                    NewValue = $"Created user: {user.Email}",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"成功建立帳號：{user.Name} ({user.Email})";
                return RedirectToAction(nameof(Roles));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "建立帳號過程發生錯誤，請稍後再試。");
                model.AvailableRoles = await GetAvailableRolesAsync();
                return View(model);
            }
        }

        // GET: Setting/ConvertToEmployee/5
        public async Task<IActionResult> ConvertToEmployee(int id)
        {
            
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            bool isEmployee = await _db.Employees.AnyAsync(e => e.UserId == id);
            if (isEmployee)
            {
                TempData["ErrorMessage"] = "該會員已經是員工，無法重複轉變。";
                return RedirectToAction(nameof(UserList));
            }

            var model = new ConvertEmployeeViewModel
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                EmployeeNo = await GenerateNextEmployeeNoAsync(),
                AvailableRoles = await GetAvailableRolesAsync()
            };

            return View(model);
        }

        // POST: Setting/ConvertToEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToEmployee(ConvertEmployeeViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (!string.IsNullOrEmpty(model.EmployeeNo) &&
                await _db.Employees.AnyAsync(e => e.EmployeeNo == model.EmployeeNo))
            {
                ModelState.AddModelError(nameof(model.EmployeeNo), "此員工編號已存在，請重新整理取得新編號");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await GetAvailableRolesAsync();
                return View(model);
            }

            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                var emp = new Employee
                {
                    UserId = model.UserId,
                    EmployeeNo = model.EmployeeNo.Trim(),
                    JobTitle = model.JobTitle?.Trim(),
                    HireDate = DateOnly.FromDateTime(DateTime.Now),
                    Status = "Active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _db.Employees.Add(emp);

                if (model.SelectedRoleIds != null && model.SelectedRoleIds.Any())
                {
                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        if (!await _db.UserRoles.AnyAsync(ur => ur.UserId == model.UserId && ur.RoleId == roleId))
                        {
                            _db.UserRoles.Add(new UserRole
                            {
                                UserId = model.UserId,
                                RoleId = roleId,
                                AssignedBy = currentUserId,
                                AssignedAt = DateTime.Now
                            });
                        }
                    }
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "ConvertToEmployee",
                    EntityType = "User",
                    EntityId = model.UserId.ToString(),
                    NewValue = $"Converted user {model.Email} to employee ({model.EmployeeNo})",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"成功將會員 {model.Name} 轉換為員工！";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "轉變員工過程發生錯誤，請稍後再試。");
                model.AvailableRoles = await GetAvailableRolesAsync();
                return View(model);
            }
        }

      

        // GET: Setting/UserList
        public async Task<IActionResult> UserList(string? keyword, int? roleId, string? status, string? userType = "all")
        {
            var availableRoles = await GetAvailableRolesAsync(excludeMemberRoles: false);

            // 1. 基本查詢：從 Users 出發，Left Join 到 Employees 判斷是否為員工
            var baseQuery = from u in _db.Users
                            join e in _db.Employees on u.UserId equals e.UserId into empGroup
                            from e in empGroup.DefaultIfEmpty()
                            select new { Usr = u, Emp = e };

            // 2. 類型篩選 (employee: 僅員工, member: 僅一般會員, all: 全部)
            if (userType == "employee")
            {
                baseQuery = baseQuery.Where(x => x.Emp != null);
            }
            else if (userType == "member")
            {
                baseQuery = baseQuery.Where(x => x.Emp == null);
            }

            // 3. 關鍵字搜尋
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var trimKeyword = keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(x =>
                    x.Usr.Name.ToLower().Contains(trimKeyword) ||
                    x.Usr.Email.ToLower().Contains(trimKeyword) ||
                    (x.Emp != null && x.Emp.EmployeeNo.ToLower().Contains(trimKeyword))
                );
            }

            // 4. 角色篩選
            if (roleId.HasValue && roleId.Value > 0)
            {
                baseQuery = baseQuery.Where(x => _db.UserRoles.Any(ur => ur.UserId == x.Usr.UserId && ur.RoleId == roleId.Value));
            }

            // 5. 狀態篩選
            if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(x => x.Usr.Status == status);
            }

            var rawList = await baseQuery
                .OrderByDescending(x => x.Usr.CreatedAt)
                .Select(x => new
                {
                    UserId = x.Usr.UserId,
                    Name = x.Usr.Name,
                    Email = x.Usr.Email,
                    Phone = x.Usr.Phone,
                    EmployeeNo = x.Emp != null ? x.Emp.EmployeeNo : null,
                    JobTitle = x.Emp != null ? x.Emp.JobTitle : null,
                    IsEmployee = x.Emp != null,
                    Status = x.Usr.Status,
                    CreatedAt = x.Usr.CreatedAt
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
                EmployeeNo = u.EmployeeNo ?? "無 (一般會員)",
                JobTitle = u.JobTitle ?? "無",
                IsEmployee = u.IsEmployee, // 請確保 DTO 有此欄位 (bool)
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                Roles = userRolesMap.Where(ur => ur.UserId == u.UserId).Select(ur => ur.RoleName).ToList()
            }).ToList();

            var model = new UserListViewModel
            {
                Keyword = keyword,
                SelectedRoleId = roleId,
                SelectedStatus = status,
                SelectedUserType = userType, // 請確保 ViewModel 有此欄位 (string)
                AvailableRoles = availableRoles,
                Users = usersList
            };

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
                Status = userData.User.Status,
                SelectedRoleIds = currentRoleIds,
                AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true)
            };

            return View(model);
        }

        // POST: Setting/EditUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (await _db.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId))
            {
                ModelState.AddModelError("Email", "此 Email 已被其他帳號使用");
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
                if (user == null)
                {
                    TempData["ErrorMessage"] = "找不到要修改的使用者資料。";
                    return RedirectToAction(nameof(UserList));
                }

                user.Name = model.Name.Trim();
                user.Email = model.Email.Trim();
                user.Phone = model.Phone;
                user.Status = model.Status;
                user.UpdatedAt = DateTime.Now;

                var emp = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == model.UserId);
                if (emp != null)
                {
                    emp.JobTitle = model.JobTitle?.Trim(); // 安全處置 null
                    emp.UpdatedAt = DateTime.Now;
                }

                //var oldRoles = await _db.UserRoles.Where(ur => ur.UserId == model.UserId).ToListAsync();
                //_db.UserRoles.RemoveRange(oldRoles);
                // 僅刪除員工類型的角色，保留一般會員（Member / Customer）角色
                var rolesToRemove = await (from ur in _db.UserRoles
                                           join r in _db.Roles on ur.RoleId equals r.RoleId
                                           where ur.UserId == model.UserId && r.RoleName != "Member" && r.RoleName != "Customer"
                                           select ur).ToListAsync();

                _db.UserRoles.RemoveRange(rolesToRemove);

                if (model.SelectedRoleIds != null && model.SelectedRoleIds.Any())
                {
                    foreach (var roleId in model.SelectedRoleIds)
                    {
                        _db.UserRoles.Add(new UserRole
                        {
                            UserId = model.UserId,
                            RoleId = roleId,
                            AssignedBy = currentUserId,
                            AssignedAt = DateTime.Now
                        });
                    }
                }

                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "EditUser",
                    EntityType = "User",
                    EntityId = model.UserId.ToString(),
                    NewValue = $"Updated user {user.Name} ({user.Email}), Title: {model.JobTitle}, Status: {model.Status}",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"已成功修改員工資料：{user.Name}";
                return RedirectToAction(nameof(UserList));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "更新員工資料時發生錯誤，請稍後再試。");
                model.AvailableRoles = await GetAvailableRolesAsync(excludeMemberRoles: true);
                return View(model);
            }
        }

        #region Private Helper Methods

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
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

            if (string.IsNullOrEmpty(lastEmpNo))
            {
                return $"{prefix}0001";
            }

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
                query = query.Where(r => r.RoleName != "Member" && r.RoleName != "Customer");
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