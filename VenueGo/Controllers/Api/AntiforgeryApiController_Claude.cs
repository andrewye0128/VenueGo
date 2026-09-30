using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using VenueGo.ViewModels;

namespace VenueGo.Controllers.Api
{
    // ════════════════════════════════════════════════════════════════
    //  前台（Vue）取得防偽 token（昱，9/29）
    //
    //  ── 為什麼需要 ──────────────────────────────────────────────
    //  Razor 頁面的表單由 TagHelper 自動塞一個隱藏欄位 __RequestVerificationToken。
    //  前台是 Vue，沒有 TagHelper，所以要先來這裡拿，再由 http.js 放進標頭送出。
    //
    //  ── 流程 ────────────────────────────────────────────────────
    //    1. 前台第一次要送 POST／PUT／DELETE 時，GET /api/antiforgery/token
    //    2. 這裡把 token 寫進一個「JS 讀得到」的 Cookie：XSRF-TOKEN
    //       （同時框架會另外寫一個 JS 讀不到的 Cookie，兩個要對得上才算通過）
    //    3. http.js 從 Cookie 讀出來，放進 RequestVerificationToken 標頭
    //       這是 ASP.NET Core 預設會去找的標頭名稱，所以 Program.cs 不用改任何設定。
    //
    //  ── 為什麼放在 /api 底下 ────────────────────────────────────
    //  開發時 Vite 只把 /api 開頭的請求轉給後端（vite.config.js 的 server.proxy）。
    //
    //  ⚠️ token 綁定「拿的時候是誰」。登入、登出之後舊的會失效（後端回 400），
    //     http.js 會自動重拿一次、重送一次。
    // ════════════════════════════════════════════════════════════════
    [ApiController]
    [Route("api/antiforgery")]
    public sealed class AntiforgeryApiController(IAntiforgery antiforgery) : ControllerBase
    {
        /// <summary>前台讀的 Cookie 名稱。改了要同步改 Front-Web/src/api/http.js 的 XSRF_COOKIE。</summary>
        public const string CookieName = "XSRF-TOKEN";

        [HttpGet("token")]
        public IActionResult GetToken()
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);

            Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false,                  // 刻意關掉：要讓前台的 JS 讀得到
                Secure = true,
                SameSite = SameSiteMode.Strict,    // 別的網站發的請求不會帶這個 Cookie
                Path = "/"
            });

            return Ok(ApiResultVM.Ok());
        }
    }
}
