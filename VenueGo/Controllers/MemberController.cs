using Microsoft.AspNetCore.Authorization;
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
    [EmployeeAuthorize()] 
    public class MemberController : Controller
    {
        private readonly dbVenueContext _db;

        public MemberController(dbVenueContext db)
        {
            _db = db;
        }

        // GET: Member/Index (僅列出擁有「會員角色」的使用者)
        public async Task<IActionResult> Index(string? keyword, string? status, int page = 1, int pageSize = 10)
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
                    (u.Phone != null && u.Phone.Contains(trimKeyword))
                );
            }

            // 4. 狀態篩選
            if (!string.IsNullOrWhiteSpace(status))
            {
                baseQuery = baseQuery.Where(u => u.Status == status);
            }

            // 5. 計算總筆數與總頁數
            var totalCount = await baseQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, page); // 防止傳入小於 1 的頁碼

            // 6. 加入排序與分頁 Skip/Take
            var rawList = await baseQuery
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                Users = rawList,
                // 傳遞分頁資訊給 View
                CurrentPage = page,
                TotalPages = totalPages,
                TotalCount = totalCount
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

        // GET: Member/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. 透過 UserId 連接 Users 與 Employees 資料表
            var userData = await (from u in _db.Users
                                  join e in _db.Employees on u.UserId equals e.UserId into empGroup
                                  from emp in empGroup.DefaultIfEmpty() // Left Join
                                  where u.Email == userEmail
                                  select new
                                  {
                                      User = u,
                                      Employee = emp
                                  }).FirstOrDefaultAsync();

            if (userData == null)
            {
                return NotFound("找不到使用者資料");
            }

            var user = userData.User;
            var employee = userData.Employee;

            // 2. 取得使用者角色列表
            var userRoles = await (from ur in _db.UserRoles
                                   join r in _db.Roles on ur.RoleId equals r.RoleId
                                   where ur.UserId == user.UserId
                                   select r.RoleName).ToListAsync();

            var model = new UserProfileViewModel
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                // 從相應的 Employee 取得資料
                EmployeeNo = employee?.EmployeeNo ?? "無",
                JobTitle = employee?.JobTitle ?? "一般會員",
                Roles = userRoles
            };

            return View(model);
        }

        // POST: Member/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
            if (user == null)
            {
                return NotFound();
            }

            // 1. 更新基本資料
            user.Name = model.Name;
            user.Phone = model.Phone;
            user.UpdatedAt = DateTime.Now;

            // 查詢對應的 Employee 資料（用於驗證失敗時重新呈現 View）
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.UserId);

            // 2. 處理密碼修改邏輯
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (string.IsNullOrEmpty(model.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "欲修改密碼，請輸入舊密碼。");
                    model.EmployeeNo = employee?.EmployeeNo ?? "無";
                    model.JobTitle = employee?.JobTitle ?? "一般會員";
                    return View(model);
                }

                bool isPasswordValid = PasswordHelper.VerifyPassword(model.CurrentPassword, user.PasswordHash);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "舊密碼輸入錯誤。");
                    model.EmployeeNo = employee?.EmployeeNo ?? "無";
                    model.JobTitle = employee?.JobTitle ?? "一般會員";
                    return View(model);
                }

                user.PasswordHash = PasswordHelper.HashPassword(model.NewPassword);
            }

            // 3. 紀錄 AuditLog
            int currentUserId = GetCurrentUserId();
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = currentUserId,
                Action = "UpdateProfile",
                EntityType = "User",
                EntityId = user.UserId.ToString(),
                NewValue = $"Updated profile for {user.Email}",
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "個人資料已成功更新！";
            return RedirectToAction(nameof(Profile));
        }


        #endregion
    }
}