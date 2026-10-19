using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Principal;
using VenueGo.Data;
using VenueGo.Extensions;

namespace VenueGo.Services.Auth
{
    /// <summary>
    /// 從驗證 Cookie 的 Claims 讀取目前登入者。
    /// <para>
    /// Claims 由 AccountController 登入成功時寫入，
    /// 其中 ClaimTypes.NameIdentifier 存的是 Users.UserId。
    /// </para>
    /// </summary>
    public sealed class CurrentUserService(IHttpContextAccessor contextAccessor) : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor = contextAccessor;

        // ════════════════════════════════════════════════════════
        //  EmployeeId 的「請求內快取」
        //
        //  本類別在 Program.cs 註冊為 Scoped，一個 HTTP 請求只會建立一個
        //  實例，所以用欄位存查詢結果就夠，不必動用 HttpContext.Items。
        //
        //  ⚠️ _employeeIdLoaded 這個旗標不能省。
        //     「查過了，但這個人不是員工」的結果也是 null，
        //     只用 _employeeId == null 判斷的話，非員工的每一次存取
        //     都會再打一次資料庫，等於沒有快取到。
        // ════════════════════════════════════════════════════════
        //private int? _employeeId;
        //private bool _employeeIdLoaded;

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => Principal.IsAuthenticated();

        /// <summary>
        /// 目前登入者的 Users.UserId。未登入回傳 null。
        /// </summary>
        public int? UserId
        {
            get
            {
                // 未登入就直接回 null，不必往下做
                if (Principal.IsAuthenticated() != true) return null;

                // ClaimTypes.NameIdentifier 是 AccountController 登入成功時
                // 寫進 Cookie 的 Users.UserId
                return Principal.GetUserId();
            }
        }

        public string? UserName => Principal.GetUserName();

        public string? EmployeeNo => Principal.GetEmployeeNo();

        /// <summary>
        /// 目前登入者的 Employees.EmployeeId。未登入或非員工回傳 null。
        /// </summary>
        public int? EmployeeId
        {
            get
            {
                if (Principal.IsAuthenticated() != true) return null;

                return Principal.GetEmployeeId();
            }
        }

    }
}
