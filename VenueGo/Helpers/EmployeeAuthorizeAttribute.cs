using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;

namespace VenueGo.Helpers
{
    /// <summary>
    /// 驗證是否為在職員工，可指定特定角色權限 (例如 Admin)
    /// 使用範例：[EmployeeAuthorize] 或 [EmployeeAuthorize("Admin", "SystemAdmin")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class EmployeeAuthorizeAttribute : TypeFilterAttribute
    {
        public EmployeeAuthorizeAttribute(params string[] roles) : base(typeof(EmployeeAuthorizeFilter))
        {
            Arguments = new object[] { roles };
        }
    }

    public class EmployeeAuthorizeFilter : IAsyncActionFilter
    {
        private readonly dbVenueContext _db;
        private readonly string[] _requiredRoles;

        public EmployeeAuthorizeFilter(dbVenueContext db, string[] requiredRoles)
        {
            _db = db;
            _requiredRoles = requiredRoles;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // -------------------------------------------------------------
            // 【關鍵防護 1】若 Action 或 Controller 標記了 [AllowAnonymous]，直接放行
            // -------------------------------------------------------------
            if (context.ActionDescriptor.EndpointMetadata.Any(em => em is AllowAnonymousAttribute))
            {
                await next();
                return;
            }

            // -------------------------------------------------------------
            // 【關鍵防護 2】若目標為 Account/Login 或 Account/Logout，直接放行，徹底防止無限轉址迴圈
            // -------------------------------------------------------------
            var controllerName = context.RouteData.Values["controller"]?.ToString();
            var actionName = context.RouteData.Values["action"]?.ToString();

            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(actionName, "Login", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionName, "Logout", StringComparison.OrdinalIgnoreCase)))
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;

            // -------------------------------------------------------------
            // 1. 檢查是否已透過 Cookie 認證登入
            // -------------------------------------------------------------
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                RedirectToLogin(context, "請先登入系統。");
                return;
            }

            // -------------------------------------------------------------
            // 2. 解析 Claims 中的 UserId
            // -------------------------------------------------------------
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                RedirectToLogin(context, "登入身分識別無效，請重新登入。");
                return;
            }

            // -------------------------------------------------------------
            // 3. 檢查資料庫：會員帳號是否存在且狀態為 Active
            // -------------------------------------------------------------
            var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (dbUser == null || dbUser.Status != "Active")
            {
                RedirectToLogin(context, "您的帳號已被停用或不存在。");
                return;
            }

            // -------------------------------------------------------------
            // 4. 檢查資料庫：是否為在職員工 (Employees.Status == Active)
            // -------------------------------------------------------------
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
            if (employee == null || employee.Status != "Active")
            {
                RedirectToLogin(context, "您非系統在職員工（或處於離職/留停狀態），無權限存取後台。");
                return;
            }

            // -------------------------------------------------------------
            // 5. 檢查角色權限（若屬性有指定特定角色，如 "Admin"）
            // -------------------------------------------------------------
            if (_requiredRoles != null && _requiredRoles.Length > 0)
            {
                // 明確的 EF Core Join 語法，防止 AsyncEnumerable 類型推導失敗
                var userRoles = await _db.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .Join(_db.Roles,
                          ur => ur.RoleId,
                          r => r.RoleId,
                          (ur, r) => r.RoleName)
                    .ToListAsync();

                // 判斷當前使用者是否具備任一被要求的角色權限
                bool hasPermission = _requiredRoles.Any(role => userRoles.Contains(role));
                if (!hasPermission)
                {
                    var controller = context.Controller as Controller;
                    if (controller != null)
                    {
                        controller.TempData["ErrorMessage"] = "您的權限不足，無法存取該功能。";
                    }

                    // 【關鍵修正】權限不足時轉向公開的首頁（Home/Index），絕對不能轉回需要 Admin 權限的頁面，否則會死迴圈
                    context.Result = new RedirectToActionResult("Index", "Home", null);
                    return;
                }
            }

            // 驗證全數通過，放行執行原本的 Action
            await next();
        }

        /// <summary>
        /// 統一處理解決未登入/無權限時，彈出訊息並導向登入頁
        /// </summary>
        private void RedirectToLogin(ActionExecutingContext context, string message)
        {
            var controller = context.Controller as Controller;
            if (controller != null)
            {
                controller.TempData["ErrorMessage"] = message;
            }

            context.Result = new RedirectToActionResult("Login", "Account", null);
        }
    }
}