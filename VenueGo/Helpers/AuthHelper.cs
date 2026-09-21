using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Models.Entities;

namespace VenueGo.Helpers
{
    /// <summary>
    /// 身分認證與權限檢查輔助工具類別
    /// 提供 Controller、Filter 與 View (.cshtml) 快速查詢使用者資訊與角色權限
    /// </summary>
    public static class AuthHelper
    {
        /// <summary>
        /// 從目前登入者的 Claims 中取得 UserId
        /// </summary>
        /// <param name="user">HttpContext.User 或 ClaimsPrincipal</param>
        /// <returns>回傳 UserId (int?)；若未登入或解析失敗則回傳 null</returns>
        public static int? GetCurrentUserId(ClaimsPrincipal user)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
            {
                return null;
            }

            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }

            return null;
        }

        /// <summary>
        /// 檢查目前登入者是否為「在職員工 (Users.Status == Active 且 Employees.Status == Active)」
        /// </summary>
        /// <param name="user">ClaimsPrincipal (帶入 User 即可)</param>
        /// <param name="db">資料庫 context (dbVenueContext)</param>
        /// <returns>若為在職員工，回傳 Employee 實體物件；若否則回傳 null</returns>
        public static async Task<Employee?> GetActiveEmployeeAsync(ClaimsPrincipal user, dbVenueContext db)
        {
            int? userId = GetCurrentUserId(user);
            if (!userId.HasValue)
            {
                return null;
            }

            // 1. 檢查 Users 資料表：帳號是否存在且狀態為 Active
            var dbUser = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (dbUser == null || dbUser.Status != "Active")
            {
                return null;
            }

            // 2. 檢查 Employees 資料表：是否為在職員工 (Status == Active)
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.UserId == userId.Value);
            if (employee == null || employee.Status != "Active")
            {
                return null;
            }

            return employee;
        }

        /// <summary>
        /// 檢查目前登入者是否擁有指定的角色（支援同時帶入多個角色，滿足其一即回傳 true）
        /// </summary>
        /// <param name="user">ClaimsPrincipal (帶入 User 即可)</param>
        /// <param name="db">資料庫 context (dbVenueContext)</param>
        /// <param name="requiredRoles">允許的角色名稱列表，例如："Admin", "Manager"</param>
        /// <returns>若擁有一項或多項相符角色回傳 true；若無權限或未登入回傳 false</returns>
        public static async Task<bool> HasRoleAsync(ClaimsPrincipal user, dbVenueContext db, params string[] requiredRoles)
        {
            // 若傳入未指定任何角色要求，代表不限制，直接允許
            if (requiredRoles == null || requiredRoles.Length == 0)
            {
                return true;
            }

            int? userId = GetCurrentUserId(user);
            if (!userId.HasValue)
            {
                return false;
            }

            // 查詢使用者在 UserRoles 與 Roles 中擁有的角色名稱列表
            var userRoles = await _dbUserRolesList(db, userId.Value);

            // 判斷當前使用者角色列表是否與要求之角色有任何重疊
            return requiredRoles.Any(role => userRoles.Contains(role));
        }

        #region Private Helper

        private static async Task<List<string>> _dbUserRolesList(dbVenueContext db, int userId)
        {
            return await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles,
                      ur => ur.RoleId,
                      r => r.RoleId,
                      (ur, r) => r.RoleName)
                .ToListAsync();
        }

        #endregion
    }
}