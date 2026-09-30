using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Principal;
using VenueGo.Data;
using VenueGo.Helpers;

namespace VenueGo.Services.Auth
{
    /// <summary>
    /// 從驗證 Cookie 的 Claims 讀取目前登入者。
    /// <para>
    /// Claims 由 AccountController 登入成功時寫入，
    /// 其中 ClaimTypes.NameIdentifier 存的是 Users.UserId。
    /// </para>
    /// </summary>
    public class CurrentUserService(IHttpContextAccessor contextAccessor) : ICurrentUserService
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
        /// 同一個請求內只會查一次資料庫。
        /// </summary>
        public int? EmployeeId
        {
            get
            {
                if (Principal.IsAuthenticated() != true) return null;

                return Principal.GetEmployeeId();
            }
        }

        /// <summary>
        /// 目前登入者的 Employees.EmployeeId。未登入或非員工回傳 null。
        /// 同一個請求內只會查一次資料庫。
        /// </summary>
        //public int? EmployeeId
        //{
        //    get
        //    {
        //        // 這個請求已經查過了就直接給答案
        //        if (_employeeIdLoaded) return _employeeId;

        //        // 先標記「查過了」。下面不論從哪一行 return，
        //        // 同一個請求內的第二次存取都不會再進來查。
        //        _employeeIdLoaded = true;
        //        _employeeId = null;

        //        if (Principal.IsAuthenticated() != true) return null;

        //        // ── 改用 UserId 查，不再用 EmployeeNo ──────────────
        //        //  原本是拿 EmployeeNo（工號字串）比對 Employees.EmployeeNo。
        //        //  改用 UserId 的三個理由：
        //        //    1. UserId 是 int 主鍵，一定有索引，比字串比對可靠也快。
        //        //    2. AccountController 寫 Claim 時是 employee.EmployeeNo ?? ""，
        //        //       理論上可能寫進空字串；UserId 則一定有值。
        //        //    3. 工號格式是登入系統那邊決定的，哪天改格式
        //        //       （例如變成 "EMP-0001"）這裡會靜默失效，且不會報錯。
        //        //       UserId 不會有這個問題。
        //        var userId = Principal.GetUserId();
        //        if (userId == null) return null;

        //        int uid = userId.Value;   // 拆成非 nullable，產生的 SQL 單純一點

        //        // 只 Select 需要的那一個欄位，不把整個 Employee 實體撈回來，
        //        // EF 也就不必追蹤它（這裡只是讀，不會改）。
        //        // 轉成 (int?) 是為了讓「查不到」時回傳 null 而不是 0——
        //        // 結果雖然一樣會被 RejectIfNotEmployee() 擋掉，
        //        // 但 null 的語意才是「沒有這個人」。
        //        _employeeId = _db.Employees
        //                         .Where(e => e.UserId == uid)
        //                         .Select(e => (int?)e.EmployeeId)
        //                         .FirstOrDefault();

        //        return _employeeId;
        //    }
        //}

    }
}
