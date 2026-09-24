using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace VenueGo.Services.Auth
{
    /// <summary>
    /// 從驗證 Cookie 的 Claims 讀取目前登入者。
    /// <para>
    /// Claims 由 AccountController 登入成功時寫入，
    /// 其中 ClaimTypes.NameIdentifier 存的是 Users.UserId。
    /// </para>
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        /// <summary>自訂 Claim 的名稱，與 AccountController 寫入時一致。</summary>
        private const string EmployeeNoClaim = "EmployeeNo";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public int? UserId
        {
            get
            {
                var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                // 未登入時 value 為 null，TryParse 會回傳 false，
                // 因此這裡不會拋例外，呼叫端只需判斷 null。
                return int.TryParse(value, out var userId) ? userId : null;
            }
        }

        public string? UserName => Principal?.FindFirstValue(ClaimTypes.Name);

        public string? EmployeeNo => Principal?.FindFirstValue(EmployeeNoClaim);
    }
}