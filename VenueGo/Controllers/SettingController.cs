using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // 1. 角色權限列表頁面
        public async Task<IActionResult> Roles()
        {
            var roles = await _db.Roles.ToListAsync();
            var model = new List<RoleListItemViewModel>();

            foreach (var r in roles)
            {
                int userCount = await _db.UserRoles.CountAsync(ur => ur.RoleId == r.RoleId);

                var permNames = await (from rp in _db.RolePermissions
                                       join p in _db.Permissions on rp.PermissionId equals p.PermissionId
                                       where rp.RoleId == r.RoleId && rp.Status && p.Status
                                       select p.PermissionName).ToListAsync();

                model.Add(new RoleListItemViewModel
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Description = r.Description,
                    Status = r.Status,
                    UserCount = userCount,
                    PermissionNames = permNames
                });
            }

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

            var allPermissions = await _db.Permissions
                .Where(p => p.Status)
                .Select(p => new PermissionOptionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionCode = p.PermissionCode,
                    PermissionName = p.PermissionName,
                    Description = p.Description,
                    Status = p.Status
                }).ToListAsync();

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
        public async Task<IActionResult> EditRole(RoleEditViewModel model, int currentUserId)
        {
            if (!ModelState.IsValid)
            {
                model.AvailablePermissions = await _db.Permissions
                    .Where(p => p.Status)
                    .Select(p => new PermissionOptionDto
                    {
                        PermissionId = p.PermissionId,
                        PermissionCode = p.PermissionCode,
                        PermissionName = p.PermissionName,
                        Description = p.Description,
                        Status = p.Status
                    }).ToListAsync();

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


        // 自動產生下一個員工編號 (例如: EMP0001)
        private async Task<string> GenerateNextEmployeeNoAsync()
        {
            const string prefix = "EMP";

            // 找出目前 EMP 開頭的最大編號
            var lastEmpNo = await _db.Employees
                .Where(e => e.EmployeeNo.StartsWith(prefix))
                .OrderByDescending(e => e.EmployeeNo)
                .Select(e => e.EmployeeNo)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(lastEmpNo))
            {
                return $"{prefix}0001"; // 第一筆預設編號
            }

            // 擷取數字部分並 +1 (例如 EMP0005 -> 5 -> 6 -> EMP0006)
            var numberPart = lastEmpNo.Substring(prefix.Length);
            if (int.TryParse(numberPart, out int currentNum))
            {
                return $"{prefix}{(currentNum + 1):D4}"; // :D4 代表補足 4 位數
            }

            // 若解析失敗，退回依據總比數產生
            int count = await _db.Employees.CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }
        // GET: Setting/CreateUser
        public async Task<IActionResult> CreateUser()
        {
            var roles = await _db.Roles
                .Where(r => r.Status)
                .Select(r => new RoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Status = r.Status
                }).ToListAsync();

            var model = new RegisterUserViewModel
            {
                AvailableRoles = roles,
                EmployeeNo = await GenerateNextEmployeeNoAsync() // 自動帶入預設員工代碼
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterUserViewModel model, int currentUserId)
        {
            if (await _db.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "此 Email 已被註冊使用");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _db.Roles
                    .Where(r => r.Status)
                    .Select(r => new RoleOptionDto
                    {
                        RoleId = r.RoleId,
                        RoleName = r.RoleName,
                        Status = r.Status
                    }).ToListAsync();

                return View(model);
            }

            // 開啟資料庫交易 (Transaction)
            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1. 建立 User 實體
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
                await _db.SaveChangesAsync(); // 取得生成的 user.UserId

                // 2. 新增 Employee 紀錄
                if (!string.IsNullOrEmpty(model.EmployeeNo))
                {
                    var emp = new Employee
                    {
                        UserId = user.UserId,
                        EmployeeNo = model.EmployeeNo,
                        JobTitle = model.JobTitle.Trim(),
                        HireDate = DateOnly.FromDateTime(DateTime.Now),
                        Status = "Active",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _db.Employees.Add(emp);
                }

                // 3. 綁定角色 (UserRole)
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

                // 4. 寫入 AuditLog (僅此一筆紀錄)
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserId,
                    Action = "CreateUser",
                    EntityType = "User",
                    EntityId = user.UserId.ToString(),
                    NewValue = $"Created user: {user.Email}",
                    CreatedAt = DateTime.Now
                });

                // 批次寫入 Employee, UserRole 與 AuditLog
                await _db.SaveChangesAsync();

                // 提交交易
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"成功建立帳號：{user.Name} ({user.Email})";
                return RedirectToAction(nameof(Roles));
            }
            catch (Exception)
            {
                // 發生異常時自動復原資料庫變更
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "建立帳號過程發生錯誤，請稍後再試。");

                model.AvailableRoles = await _db.Roles
                    .Where(r => r.Status)
                    .Select(r => new RoleOptionDto
                    {
                        RoleId = r.RoleId,
                        RoleName = r.RoleName,
                        Status = r.Status
                    }).ToListAsync();

                return View(model);
            }
        }
    }
}