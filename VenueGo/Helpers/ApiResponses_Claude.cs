using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using VenueGo.ViewModels;

namespace VenueGo.Helpers
{
    // ════════════════════════════════════════════════════════════════
    //  全站 API（網址 /api 開頭）共用的失敗回應（昱，9/29）
    //
    //  組員開發注意事項規定：給前台的 API 一律回 ApiResult。
    //  但有兩種失敗不是 Action 自己回的，而是框架在「進 Action 之前」就回掉了，
    //  預設的格式都不是 ApiResult：
    //
    //    1. [ApiController] 的自動模型驗證 → 預設回 ProblemDetails
    //    2. 沒登入／權限不足 → Cookie 驗證預設「導向登入頁」，前台拿到的是一整頁 HTML
    //
    //  這個檔案把兩者都改成 ApiResult。要在 Program.cs 掛上才會生效（共三行，見各方法的說明）。
    //  沒掛也不會壞：前台 http.js 本來就接得住 ProblemDetails，只是欄位錯誤不會是小寫開頭。
    // ════════════════════════════════════════════════════════════════
    public static class ApiResponses
    {
        // ── 1. 模型驗證失敗 ───────────────────────────────────────

        /// <summary>
        /// [ApiController] 模型驗證失敗時的回應。回傳形狀：
        ///   400 { success: false, message: "第一則錯誤", errorCode: "ValidationFailed",
        ///         data: { errors: { starRating: ["請選擇星等"] } } }
        ///
        /// message 用「第一則錯誤」，跟前台 http.js 原本處理 ProblemDetails 的做法一樣，
        /// 所以組員的頁面顯示出來的字不會變。
        ///
        /// Program.cs 要加（放在 AddControllersWithViews 下面）：
        ///   builder.Services.Configure&lt;Microsoft.AspNetCore.Mvc.ApiBehaviorOptions&gt;(o =>
        ///       o.InvalidModelStateResponseFactory = ApiResponses.InvalidModelState);
        /// </summary>
        public static IActionResult InvalidModelState(ActionContext context)
        {
            var errors = new Dictionary<string, string[]>();

            foreach (var (key, entry) in context.ModelState)
            {
                if (entry.Errors.Count == 0) continue;

                // JSON 本身格式錯誤時，key 長得像 "$.starRating"，訊息是英文的技術說明，不適合給使用者看
                bool isJsonError = key.StartsWith('$');
                string name = ToCamelPath(isJsonError ? key.TrimStart('$', '.') : key);

                if (isJsonError)
                    errors[name] = new[] { JsonFormatMessage };
                else if (name == NoField)
                    errors[name] = new[] { EmptyBodyMessage };   // key 是空字串：通常是整個本體沒送
                else
                    errors[name] = entry.Errors.Select(e => e.ErrorMessage).ToArray();
            }

            if (errors.Count == 0)
                errors[NoField] = new[] { EmptyBodyMessage };

            return new BadRequestObjectResult(ValidationFailed(errors));
        }

        /// <summary>
        /// 本體整個沒送（收 JSON 的參數是 null）。
        /// 參數要宣告成可為 null（例如 ReviewCreateForVisitVM? vm），框架才會把 null 交給 Action，
        /// 不然框架會自己產生一則英文的「The vm field is required.」。
        /// 用法：if (vm == null) return BadRequest(ApiResponses.EmptyBody());
        /// </summary>
        public static ApiResult<object> EmptyBody()
            => ValidationFailed(new Dictionary<string, string[]> { [NoField] = new[] { EmptyBodyMessage } });

        private static ApiResult<object> ValidationFailed(Dictionary<string, string[]> errors) => new()
        {
            Success = false,
            Message = errors.Values.First()[0],
            ErrorCode = "ValidationFailed",
            Data = new { errors }
        };

        /// <summary>不屬於任何欄位的錯誤用這個 key（前台顯示在表單最上方）。</summary>
        public const string NoField = "_";

        private const string JsonFormatMessage = "送出的資料格式不正確，請重新整理頁面後再試一次";
        private const string EmptyBodyMessage = "沒有收到資料，請重新整理頁面後再試一次";

        /// <summary>
        /// 欄位名稱轉成小寫開頭。System.Text.Json 只會轉「屬性名」，不會轉「Dictionary 的 key」，
        /// 不轉的話前端拿到的是 StarRating。巢狀的（Items[0].Name）每一段都轉。
        /// </summary>
        private static string ToCamelPath(string key)
        {
            if (string.IsNullOrEmpty(key)) return NoField;

            var parts = key.Split('.').Select(p => JsonNamingPolicy.CamelCase.ConvertName(p)).ToArray();
            return string.Join('.', parts);
        }

        // ── 2. 沒登入／權限不足 ──────────────────────────────────

        // 預設的處理方式（非 /api 網址照舊用它：導向登入頁；AJAX 請求回 401）
        private static readonly CookieAuthenticationEvents Defaults = new();

        /// <summary>
        /// 沒登入。/api 開頭 → 401 ＋ ApiResult；其他網址照舊導向登入頁。
        ///
        /// Program.cs 的 AddCookie(options => { ... }) 裡面要加：
        ///   options.Events.OnRedirectToLogin = ApiResponses.RedirectToLogin;
        /// </summary>
        public static Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
        {
            if (IsApi(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(ApiResultVM.Fail("請先登入", "NotLoggedIn"));
            }
            return Defaults.RedirectToLogin(context);
        }

        /// <summary>
        /// 已登入但角色不對（例如員工帳號打會員專用的 API）。/api 開頭 → 403 ＋ ApiResult；其他照舊。
        ///
        /// Program.cs 的 AddCookie(options => { ... }) 裡面要加：
        ///   options.Events.OnRedirectToAccessDenied = ApiResponses.RedirectToAccessDenied;
        /// </summary>
        public static Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
        {
            if (IsApi(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(ApiResultVM.Fail("這個功能需要其他身分才能使用", "Forbidden"));
            }
            return Defaults.RedirectToAccessDenied(context);
        }

        private static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");
    }
}
