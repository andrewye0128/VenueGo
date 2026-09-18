using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models;
using VenueGo.Models.Entities;
using VenueGo.ViewModels;

namespace VenueGo.Controllers
{
    public class AccountController : Controller
    {
        private readonly dbVenueContext _db;

        public AccountController(dbVenueContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string ipAddress = GetClientIpAddress();

            // -------------------------------------------------------------
            // 1. 搜尋使用者帳號
            // -------------------------------------------------------------
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                await WriteLoginLogAsync(null, model.Email, ipAddress, false, "帳號不存在");
                ModelState.AddModelError(string.Empty, "帳號或密碼錯誤。");
                return View(model);
            }

            // -------------------------------------------------------------
            // 2. 檢查資料庫鎖定狀態 (LockedUntil)
            // -------------------------------------------------------------
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
            {
                var remainingMinutes = Math.Ceiling((user.LockedUntil.Value - DateTime.Now).TotalMinutes);

                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, $"帳號鎖定中（剩餘 {remainingMinutes} 分鐘）");
                ModelState.AddModelError(string.Empty, $"登入失敗次數過多，帳號鎖定中，請於 {remainingMinutes} 分鐘後再試。");
                return View(model);
            }

            // -------------------------------------------------------------
            // 3. 密碼驗證與失敗計數處理
            // -------------------------------------------------------------
            bool isPasswordValid = PasswordHelper.VerifyPassword(model.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                user.FailedLoginCount = user.FailedLoginCount+ 1;
                string reason;

                if (user.FailedLoginCount >= 5)
                {
                    user.LockedUntil = DateTime.Now.AddMinutes(15);
                    reason = "密碼連續錯誤達 5 次，觸發帳號鎖定 15 分鐘";
                    ModelState.AddModelError(string.Empty, "密碼錯誤達 5 次，帳號已鎖定 15 分鐘！");
                }
                else
                {
                    int remainingAttempts = 5 - user.FailedLoginCount;
                    reason = $"帳號或密碼錯誤（連續失敗第 {user.FailedLoginCount} 次）";
                    ModelState.AddModelError(string.Empty, $"帳號或密碼錯誤（剩餘可嘗試次數：{remainingAttempts} 次）。");
                }

                user.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();

                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, reason);
                return View(model);
            }

            // -------------------------------------------------------------
            // 4. 檢查帳號狀態與員工權限
            // -------------------------------------------------------------
            // 4a. 檢查會員基礎帳號狀態
            if (user.Status != "Active")
            {
                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, "會員帳號已被停用");
                ModelState.AddModelError(string.Empty, "此帳號已被停用，請聯繫系統管理員。");
                return View(model);
            }

            // 4b. 取得員工資料與後台管理角色
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == user.UserId);
            var userRoles = await (from ur in _db.UserRoles
                                   join r in _db.Roles on ur.RoleId equals r.RoleId
                                   where ur.UserId == user.UserId && r.RoleName != "Member" && r.RoleName != "Customer"
                                   select r.RoleName).ToListAsync();

            // 4c. 檢查是否為員工資料與在職狀態 (限制必須為 Active)
            if (employee == null)
            {
                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, "非後台員工帳號嘗試登入");
                ModelState.AddModelError(string.Empty, "登入失敗：此帳號非系統員工帳號。");
                return View(model);
            }

            if (employee.Status != "Active")
            {
                string statusText = employee.Status switch
                {
                    "Resigned" => "已離職",
                    "OnLeave" => "留職停薪",
                    _ => "狀態異常"
                };

                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, $"員工狀態非啟用 ({statusText})");
                ModelState.AddModelError(string.Empty, $"登入失敗：該員工帳號目前為「{statusText}」狀態，無法登入後台。");
                return View(model);
            }

            // 4d. 檢查是否擁有後台管理角色
            if (!userRoles.Any())
            {
                await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, false, "員工未配給後台角色權限");
                ModelState.AddModelError(string.Empty, "登入失敗：此帳號未具備後台操作權限。");
                return View(model);
            }

            // -------------------------------------------------------------
            // 5. 登入成功：歸零失敗計數、寫入 LoginLog 與更新最後登入時間
            // -------------------------------------------------------------
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.Now;
            user.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            await WriteLoginLogAsync(user.UserId, model.Email, ipAddress, true, null);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("EmployeeNo", employee.EmployeeNo ?? "")
            };

            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            TempData["SuccessMessage"] = $"歡迎回來，{user.Name}！";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("UserList", "Setting");
        }

        // -------------------------------------------------------------
        // 私有輔助方法：紀錄 LoginLog 與 取得 Client IP
        // -------------------------------------------------------------
        private async Task WriteLoginLogAsync(int? userId, string account, string ipAddress, bool result, string? failureReason)
        {
            var log = new LoginLog
            {
                UserId = userId,
                LoginAccount = account,
                IpAddress = ipAddress,
                LoginTime = DateTime.UtcNow,
                Result = result,
                FailureReason = failureReason
            };

            _db.LoginLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        private string GetClientIpAddress()
        {
            var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
            {
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            }
            return ip ?? "127.0.0.1";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "您已成功登出系統。";
            return RedirectToAction("Login", "Account");
        }
    }
}