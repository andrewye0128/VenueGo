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
    [EmployeeAuthorize("Admin", "Manager")] // 👈 傳入兩個獨立字串
    public class MemberController : Controller
    {
        private readonly dbVenueContext _db;

        public MemberController(dbVenueContext db)
        {
            _db = db;
        }

        // GET: Member/Index (僅列出擁有「會員角色」的使用者)
        public async Task<IActionResult> Index(string? keyword, string? status)
        {
            // 1. 查詢所有擁有 "Member" 角色的 UserId 集合
            var memberUserIds = _db.UserRoles
                .Where(ur => _db.Roles.Any(r => r.RoleId == ur.RoleId && r.RoleName == "Member"))
                .Select(ur => ur.UserId);

            // 2. 以 Users 資料表為主，條件為「UserId 存在於會員角色集合中」
            var baseQuery = _db.Users
                .Where(u => memberUserIds.Contains(u.UserId));

            // 3. 關鍵字搜尋 (姓名、Email、電話)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var trimKeyword = keyword.Trim().ToLower();
                baseQuery = baseQuery.Where(u =>
                    u.Name.ToLower().Contains(trimKeyword) ||
                    u.Email.ToLower().Contains(trimKeyword) ||
                    u.Phone.Contains(trimKeyword)
                );
            }

            // 4. 狀態篩選
            if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(u => u.Status == status);
            }

            // 5. 投影到 UserListItemDto，並計算是否同時為員工
            var rawList = await baseQuery
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new UserListItemDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone ?? "未提供",
                    IsEmployee = _db.Employees.Any(e => e.UserId == u.UserId),
                    Status = u.Status,
                    LockedUntil = u.LockedUntil,
                    CreatedAt = u.CreatedAt,
                    Roles = new List<string> { "Member" }
                }).ToListAsync();

            var model = new UserListViewModel
            {
                Keyword = keyword,
                SelectedStatus = status,
                SelectedUserType = "member",
                Users = rawList
            };

            return View(model);
        }

        // POST: Member/UpdateMemberStatus (變更會員狀態，如停權/啟用)
        [EmployeeAuthorize("Admin")] // 僅限管理員存取
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberStatus(int userId, string status)
        {
            int currentUserId = GetCurrentUserId();

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "查無該會員資料。";
                return RedirectToAction(nameof(Index));
            }

            user.Status = status; // Active / Suspended / Inactive
            user.UpdatedAt = DateTime.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "UpdateMemberStatus",
                EntityType = "User",
                EntityId = user.UserId.ToString(),
                NewValue = $"Updated member {user.Email} status to '{status}'",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已將會員 {user.Name} 狀態更新為：{status}";
            return RedirectToAction(nameof(Index));
        }

        // GET: Member/ConvertToEmployee/5 (將會員提升為員工)
        [EmployeeAuthorize("Admin")] // 僅限管理員存取
        public async Task<IActionResult> ConvertToEmployee(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            bool isEmployee = await _db.Employees.AnyAsync(e => e.UserId == id);
            if (isEmployee)
            {
                TempData["ErrorMessage"] = "該使用者已經是員工。";
                return RedirectToAction(nameof(Index));
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

        // POST: Member/ConvertToEmployee
        [EmployeeAuthorize("Admin")] // 僅限管理員存取
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertToEmployee(ConvertEmployeeViewModel model)
        {
            int currentUserId = GetCurrentUserId();

            if (!string.IsNullOrEmpty(model.EmployeeNo) &&
                await _db.Employees.AnyAsync(e => e.EmployeeNo == model.EmployeeNo))
            {
                ModelState.AddModelError(nameof(model.EmployeeNo), "此員工編號已存在");
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
                    NewValue = $"Converted member {model.Email} to employee ({model.EmployeeNo})",
                    CreatedAt = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"成功將會員 {model.Name} 升格為員工！";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "轉變員工過程發生錯誤。");
                model.AvailableRoles = await GetAvailableRolesAsync();
                return View(model);
            }
        }

        // GET: Member/GetDetailJson/5 (Modal 詳細資料 API)
        [HttpGet]
        public async Task<IActionResult> GetDetailJson(int id)
        {
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.Phone,
                    Birth = u.Birth != default ? u.Birth.ToString("yyyy-MM-dd") : "未填寫",
                    u.CumulativeConsumption,
                    u.CumulativeVisitTime,
                    u.NoShowCount,
                    u.Status,
                    CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();
            return Json(user);
        }

        // POST: Member/ResetMemberPassword
        [EmployeeAuthorize("Admin")] // 僅限管理員存取
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetMemberPassword(int userId)
        {
            int currentUserId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "查無該會員資料。";
                return RedirectToAction(nameof(Index));
            }

            user.PasswordHash = PasswordHelper.HashPassword("Pass@1234");
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = DateTime.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "ResetMemberPassword",
                EntityType = "User",
                EntityId = user.UserId.ToString(),
                NewValue = $"Reset password for member {user.Email}",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已將會員 {user.Name} 的密碼重置為預設密碼：Pass@1234";
            return RedirectToAction(nameof(Index));
        }

        // POST: Member/UnlockAccount
        [EmployeeAuthorize("Admin")] // 僅限管理員存取
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockAccount(int userId)
        {
            int currentUserId = GetCurrentUserId();
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.UpdatedAt = DateTime.Now;

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "UnlockMemberAccount",
                EntityType = "User",
                EntityId = user.UserId.ToString(),
                NewValue = $"Unlocked account for member {user.Email}",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"已成功解鎖會員 {user.Name} 的帳號。";
            return RedirectToAction(nameof(Index));
        }

        // GET: Member/ExportToCsv
        public async Task<IActionResult> ExportToCsv()
        {
            // 👈 已修正：匯出所有擁有 "Member" 角色的使用者
            var memberUserIds = _db.UserRoles
                .Where(ur => _db.Roles.Any(r => r.RoleId == ur.RoleId && r.RoleName == "Member"))
                .Select(ur => ur.UserId);

            var members = await _db.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("會員ID,姓名,Email,電話,狀態,累計消費,註冊時間");

            foreach (var m in members)
            {
                builder.AppendLine($"{m.UserId},\"{m.Name}\",\"{m.Email}\",\"{m.Phone}\",\"{m.Status}\",{m.CumulativeConsumption},\"{m.CreatedAt:yyyy-MM-dd HH:mm}\"");
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString()))
                .ToArray();

            return File(buffer, "text/csv", $"Members_Export_{DateTime.Now:yyyyMMdd}.csv");
        }

        #region Private Helpers

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

            if (string.IsNullOrEmpty(lastEmpNo)) return $"{prefix}0001";

            var numberPart = lastEmpNo.Substring(prefix.Length);
            if (int.TryParse(numberPart, out int currentNum))
            {
                return $"{prefix}{(currentNum + 1):D4}";
            }

            int count = await _db.Employees.CountAsync();
            return $"{prefix}{(count + 1):D4}";
        }

        private async Task<List<RoleOptionDto>> GetAvailableRolesAsync()
        {
            return await _db.Roles
                .Where(r => r.Status && r.RoleName != "Member" && r.RoleName != "Customer")
                .Select(r => new RoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Status = r.Status
                }).ToListAsync();
        }

        #endregion
    }
}