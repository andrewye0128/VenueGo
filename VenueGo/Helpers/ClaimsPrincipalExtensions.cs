using System.Security.Claims;

namespace VenueGo.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user) 
        {
            var identifier = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(identifier, out var userId) ? userId : null;
        }

        public static string? GetUserName(this ClaimsPrincipal user)
        {
            return user?.FindFirstValue(ClaimTypes.Name);
        }

        public static string? GetUserEmail(this ClaimsPrincipal user)
        {
            return user?.FindFirstValue(ClaimTypes.Email);
        }

        public static string? GetEmployeeNo(this ClaimsPrincipal user)
        {
            return user?.FindFirstValue("EmployeeNo");
        }

        /// <summary>
        /// 快速判斷使用者是否已登入
        /// </summary>
        public static bool IsAuthenticated(this ClaimsPrincipal user)
        {
            return user.Identity?.IsAuthenticated == true;
        }
    }
}
