using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using VenueGo.Dtos;
using VenueGo.Helpers;
using VenueGo.Services;

namespace VenueGo.Controllers
{
    // ════════════════════════════════════════════════════════════════
    //  開發用時光機（昱）
    //
    //  /DevTools/Time      操作面板（後台提示條的「調整」會用小視窗打開它）
    //  /api/dev/time       面板、提示條、前台 Vue 共用的 API
    //
    //  ⚠️ 只在開發環境可用，其他環境一律回 404。沒有掛 [Authorize]：
    //     登入本身也和時間有關（例如鎖定時間），要能在登入前就調整。
    //  ⚠️ 交件或部署前，連同 Views/Shared/*TimeMachine*_Claude.cshtml、
    //     wwwroot/js/time-machine*.js 一起刪掉。
    //
    //  【為什麼拿掉網址列的 ?add=1d、?reset=1】
    //  以前用 GET 改時間。瀏覽器會「預先載入」網址列自動完成的第一個建議，
    //  打「localhost:7078/D…」時，它可能就在背景先開了 /DevTools/Time?reset=1，
    //  時光機就被偷偷重設，提示條也跟著不見。現在一律用 POST，網址列只能「看」不能「改」。
    //  （?to=2026-10-30T16:45 還能用，但只會把時間填進面板，要按「出發」才會跳。）
    // ════════════════════════════════════════════════════════════════

    /// <summary>時光機的操作面板：/DevTools/Time</summary>
    [Route("DevTools/Time")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class TimeMachineController(
        IWebHostEnvironment environment,
        ITimeMachine timeMachine,
        ITimeService timeService,
        IFileVersionProvider fileVersionProvider) : Controller
    {
        public const string PanelView = "~/Views/Shared/TimeMachinePanel_Claude.cshtml";

        [HttpGet("")]
        public IActionResult Index(string? to)
        {
            if (!environment.IsDevelopment() || !timeMachine.IsAvailable) return NotFound();

            var vm = new TimeMachinePanelVM
            {
                State = TimeMachineState.Create(timeMachine, timeService, Request, fileVersionProvider),
                Prefill = TimeMachine.TryParseTaipeiTime(to, out DateTime target) ? target : null,
                PanelScriptUrl = fileVersionProvider.AddFileVersionToPath(Request.PathBase, "/js/time-machine-panel.js"),
            };

            // 面板的內容每秒都在變，不要讓瀏覽器拿快取的舊頁面出來
            Response.Headers.CacheControl = "no-store";
            return View(PanelView, vm);
        }
    }

    /// <summary>時光機 API：/api/dev/time。回傳格式照全組規定用 ApiResult。</summary>
    [ApiController]
    [Route("api/dev/time")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class TimeMachineApiController(
        IWebHostEnvironment environment,
        ITimeMachine timeMachine,
        ITimeService timeService,
        TimeProvider timeProvider,
        IFileVersionProvider fileVersionProvider) : ControllerBase
    {
        /// <summary>快速調整一次最多撥多少（前後各 400 天），擋掉打錯字造成的離譜數字。</summary>
        public const int MaxShiftMinutes = 400 * 24 * 60;

        private bool Unavailable => !environment.IsDevelopment() || !timeMachine.IsAvailable;

        /// <summary>目前狀態。提示條每 15 秒、切回分頁時會來問一次。</summary>
        [HttpGet("")]
        public IActionResult Get()
        {
            if (Unavailable) return NotFound();
            return Ok(CurrentState());
        }

        /// <summary>跳到指定的台北時間（年月日時分秒分開送，伺服器再檢查一次範圍）。</summary>
        // [Consumes] 的作用：只收 Content-Type: application/json。
        // 別的網站用 <form> 偷偷送過來的請求不是 JSON，會直接被擋掉（415），算是簡單的 CSRF 防護。
        [HttpPost("travel")]
        [Consumes("application/json")]
        public IActionResult Travel(TimeMachineTravelRequest? request)
        {
            if (Unavailable) return NotFound();
            if (request == null) return BadRequest(ApiResponses.EmptyBody());

            if (!TimeMachineState.TryBuildTaipeiTime(request, out DateTime target, out string? error))
                return BadRequest(ApiResult.Fail(error!, "InvalidTime"));

            JumpTo(target);
            return Ok(CurrentState($"已跳到 {target:yyyy/M/d HH:mm:ss}"));
        }

        /// <summary>往前或往後撥幾分鐘（−7 天＝−10080）。</summary>
        [HttpPost("shift")]
        [Consumes("application/json")]
        public IActionResult Shift(TimeMachineShiftRequest? request)
        {
            if (Unavailable) return NotFound();
            if (request == null) return BadRequest(ApiResponses.EmptyBody());

            if (request.Minutes == 0)
                return BadRequest(ApiResult.Fail("要撥幾分鐘？0 分鐘不用撥", "InvalidShift"));

            var delta = TimeSpan.FromMinutes(request.Minutes);
            DateTime after = timeService.Now + delta;
            if (after.Year < TimeMachineState.MinYear || after.Year > TimeMachineState.MaxYear)
                return BadRequest(ApiResult.Fail($"只能在 {TimeMachineState.MinYear}～{TimeMachineState.MaxYear} 年之間", "InvalidShift"));

            timeMachine.TravelBy(delta);
            string direction = delta > TimeSpan.Zero ? "往後" : "往前";
            return Ok(CurrentState($"已{direction}撥 {TimeMachineState.FormatSpan(delta.Duration())}"));
        }

        /// <summary>回到真實時間。本體送 {} 就好。</summary>
        [HttpPost("reset")]
        [Consumes("application/json")]
        public IActionResult Reset(TimeMachineResetRequest? request)
        {
            if (Unavailable) return NotFound();

            timeMachine.Reset();
            return Ok(CurrentState($"已回到現在 {timeService.Now:yyyy/M/d HH:mm:ss}"));
        }

        /// <summary>
        /// 精準跳到某一秒：用「目標 − 目前的網站時間（含校時偏移、不捨去毫秒）」去撥。
        /// 直接呼叫 TravelTo 的話，校時偏移量會讓結果差個幾秒。
        /// </summary>
        private void JumpTo(DateTime target)
        {
            DateTime preciseSiteNow = timeProvider.GetLocalNow().DateTime + timeService.Offset + timeMachine.TravelOffset;
            timeMachine.TravelBy(target - preciseSiteNow);
        }

        private ApiResult<TimeMachineStateDto> CurrentState(string? message = null)
            => ApiResult<TimeMachineStateDto>.Ok(
                TimeMachineState.Create(timeMachine, timeService, Request, fileVersionProvider), message);

    }

    // ════════════════════════════════════════════════════════════════
    //  DTO／VM（只給時光機用，放同一個檔案，刪的時候一起刪）
    // ════════════════════════════════════════════════════════════════

    /// <summary>跳到指定時間。年月日時分秒分開送，前端不用組字串。</summary>
    public sealed class TimeMachineTravelRequest
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public int Day { get; init; }
        public int Hour { get; init; }
        public int Minute { get; init; }
        public int Second { get; init; }
    }

    /// <summary>往前（負數）或往後（正數）撥幾分鐘。</summary>
    public sealed class TimeMachineShiftRequest
    {
        [Range(-TimeMachineApiController.MaxShiftMinutes, TimeMachineApiController.MaxShiftMinutes,
               ErrorMessage = "一次最多撥 400 天")]
        public int Minutes { get; init; }
    }

    /// <summary>回到現在不需要任何資料；有這個類別只是為了讓 [Consumes] 生效。</summary>
    public sealed class TimeMachineResetRequest { }

    /// <summary>給提示條、面板、Vue 的狀態。時間一律是台北時間的 "yyyy-MM-ddTHH:mm:ss" 字串，不帶時區。</summary>
    public sealed record TimeMachineStateDto(
        bool Available,
        bool Traveling,
        string SiteNow,
        string RealNow,
        long OffsetSeconds,
        string OffsetText,
        string Stamp,                // 用字串：Ticks 超過 JavaScript 數字能精準表示的範圍
        IReadOnlyList<int> Years,
        string PanelUrl,
        string ScriptUrl);

    /// <summary>/DevTools/Time 面板頁的 VM。</summary>
    public sealed class TimeMachinePanelVM
    {
        public required TimeMachineStateDto State { get; init; }

        /// <summary>網址帶了 ?to=… 就先填進輸入框（不會自動出發）。</summary>
        public DateTime? Prefill { get; init; }

        public required string PanelScriptUrl { get; init; }
    }

    /// <summary>狀態的組裝和檢查。抽成靜態方法，提示條的 partial 和單元測試都能直接用。</summary>
    public static class TimeMachineState
    {
        public const int MinYear = 2000;
        public const int MaxYear = 2100;

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public static TimeMachineStateDto Create(
            ITimeMachine timeMachine, ITimeService timeService, HttpRequest request, IFileVersionProvider fileVersionProvider)
        {
            DateTime siteNow = timeService.Now;
            TimeSpan offset = timeMachine.TravelOffset;
            DateTime realNow = siteNow - offset;   // 校時後的真實時間＝按「回到現在」之後會變成的時間

            // 絕對網址：前台 Vue（localhost:5173）透過 Vite 轉發來問時，也要拿到後端的網址。
            // Vite 設了 changeOrigin，轉過來的 Host 就是後端自己，所以這裡組出來的是對的。
            string origin = $"{request.Scheme}://{request.Host}{request.PathBase}";

            return new TimeMachineStateDto(
                Available: timeMachine.IsAvailable,
                Traveling: timeMachine.IsTraveling,
                SiteNow: siteNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                RealNow: realNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                OffsetSeconds: (long)offset.TotalSeconds,
                OffsetText: offset == TimeSpan.Zero ? "" : $"{(offset > TimeSpan.Zero ? "快" : "慢")} {FormatSpan(offset.Duration())}",
                Stamp: timeMachine.Stamp.ToString(),
                Years: YearOptions(realNow.Year, siteNow.Year),
                PanelUrl: origin + "/DevTools/Time",
                ScriptUrl: origin + fileVersionProvider.AddFileVersionToPath(request.PathBase, "/js/time-machine.js"));
        }

        /// <summary>年份下拉選單：去年、今年、明年（以真實時間為準）；網站時間不在裡面的話也加進去。</summary>
        public static IReadOnlyList<int> YearOptions(int realYear, int siteYear)
        {
            var years = new SortedSet<int> { realYear - 1, realYear, realYear + 1, siteYear };
            return years.ToList();
        }

        /// <summary>檢查年月日時分秒是否合理，合理就組成時間。前端也有擋，這裡是第二道。</summary>
        public static bool TryBuildTaipeiTime(TimeMachineTravelRequest r, out DateTime result, out string? error)
        {
            result = default;
            error = null;

            if (r.Year < MinYear || r.Year > MaxYear) error = $"年要在 {MinYear}～{MaxYear} 之間";
            else if (r.Month is < 1 or > 12) error = "月要在 1～12 之間";
            else if (r.Day < 1 || r.Day > DateTime.DaysInMonth(r.Year, r.Month))
                error = $"{r.Year} 年 {r.Month} 月只有 {DateTime.DaysInMonth(r.Year, r.Month)} 天";
            else if (r.Hour is < 0 or > 23) error = "時要在 0～23 之間";
            else if (r.Minute is < 0 or > 59) error = "分要在 0～59 之間";
            else if (r.Second is < 0 or > 59) error = "秒要在 0～59 之間";

            if (error != null) return false;

            result = new DateTime(r.Year, r.Month, r.Day, r.Hour, r.Minute, r.Second);
            return true;
        }

        /// <summary>「3 天 4 小時 5 分」。不到一分鐘回「不到 1 分鐘」。</summary>
        public static string FormatSpan(TimeSpan span)
        {
            span = span.Duration();
            var parts = new List<string>();
            if (span.Days > 0)    parts.Add($"{span.Days} 天");
            if (span.Hours > 0)   parts.Add($"{span.Hours} 小時");
            if (span.Minutes > 0) parts.Add($"{span.Minutes} 分");
            return parts.Count == 0 ? "不到 1 分鐘" : string.Join(" ", parts);
        }
    }
}
