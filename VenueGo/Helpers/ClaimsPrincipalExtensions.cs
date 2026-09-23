using System.Security.Claims;

namespace VenueGo.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        // ⚠️ 這個字串目前散在三個地方：
        //    AccountController 寫入時、CurrentUserService 的 EmployeeNoClaim、這裡讀取時。
        //    三份要一起改才會對，漏一份不會編譯失敗，只會安靜地讀不到值。
        //    收成一個 const 之後就只有一份。
        //    （更好的做法是放進 Models/Constants，但那是共用資料夾，要先跟組員談。）
        public const string EmployeeNoClaimType = "EmployeeNo";
        public const string EmployeeIdClaimType = "EmployeeId";   // 目前還沒有人寫入，見下面說明

        // ── 參數改成可為 null ──────────────────────────────────
        //  原本宣告成 ClaimsPrincipal（不可為 null），方法內部再寫 user?.FindFirstValue(...)。
        //  內部的 ?. 只防止方法自己炸，**擋不住呼叫端的警告**——
        //  編譯器看的是「你把一個可能是 null 的東西，傳給一個宣告為不可為 null 的參數」，
        //  那是 CS8604，發生在呼叫端，跟方法內部怎麼寫無關。
        //  把參數宣告成 ClaimsPrincipal? 才是在說「我接受 null，我會處理」。
        //  這同時是給呼叫者看的契約，不只是消警告。
        public static int? GetUserId(this ClaimsPrincipal? user) 
        {
            var identifier = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(identifier, out var userId) ? userId : null;
        }

        public static string? GetUserName(this ClaimsPrincipal? user)
            => user?.FindFirstValue(ClaimTypes.Name);

        public static string? GetUserEmail(this ClaimsPrincipal? user)
            => user?.FindFirstValue(ClaimTypes.Email);

        public static string? GetEmployeeNo(this ClaimsPrincipal? user)
            => user?.FindFirstValue(EmployeeNoClaimType);

        /// <summary>
        /// 目前登入者的 Employees.EmployeeId。
        /// ⚠️ 需要 AccountController 在登入時多寫一個 Claim 才會有值，目前還沒有。
        ///    寫入之後，CurrentUserService 就不必注入 dbVenueContext，
        ///    每個請求也少一次資料庫查詢。
        /// </summary>
        public static int? GetEmployeeId(this ClaimsPrincipal? user)
        {
            var value = user?.FindFirstValue(EmployeeIdClaimType);
            return int.TryParse(value, out var id) ? id : null;
        }


        /// <summary>
        /// 快速判斷使用者是否已登入
        /// </summary>
        public static bool IsAuthenticated(this ClaimsPrincipal? user)
            => user?.Identity?.IsAuthenticated == true;

        // ── 3. Role：已經寫進 Claim 了，只是還沒人拿來用 ──────────
        //  AccountController 登入成功時有跑
        //      foreach (var role in userRoles) claims.Add(new Claim(ClaimTypes.Role, role));
        //  所以 [Authorize(Roles = "...")] 和 User.IsInRole("Staff") 都是可用的。

        /// <summary>目前登入者的所有角色名稱。未登入或沒有角色回傳空集合。</summary>
        public static IEnumerable<string> GetRoles(this ClaimsPrincipal? user)
            => user?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();

        /// <summary>
        /// 是否具備其中任一個角色。
        ///
        /// ⚠️ 為什麼需要這個而不是直接用內建的 user.IsInRole()：
        ///    RoleNames.BackOffice 的值是 "Staff,Manager,Admin"——**一個逗號串起來的字串**。
        ///    [Authorize(Roles = ...)] 會幫你用逗號切開，但
        ///    user.IsInRole("Staff,Manager,Admin") 是拿整串去比對一個角色名，
        ///    永遠不會相等。這個方法幫你切。
        ///
        ///    同樣的坑也存在於 [EmployeeAuthorize(RoleNames.BackOffice)]——
        ///    它的 params string[] 只會收到一個元素 "Staff,Manager,Admin"，
        ///    比對必定失敗，結果是「所有人都被擋在外面」。
        ///    失敗方向是安全的（擋掉而不是放行），但症狀會很難懂。
        /// </summary>
        public static bool HasAnyRole(this ClaimsPrincipal? user, params string[] roles)
        {
            if (user is null || roles is null || roles.Length == 0) return false;

            // 每個元素都可能自己是逗號串，先全部攤平
            var wanted = roles
                .SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return user.GetRoles().Any(wanted.Contains);
        }

        /// <summary>是否為可進入後台的角色（Staff／Manager／Admin）。</summary>
        public static bool IsBackOffice(this ClaimsPrincipal? user)
            => user.HasAnyRole(Models.Constants.RoleNames.BackOffice);
    }
}
