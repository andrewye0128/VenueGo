using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.ReviewDtos.MyReviews;
using VenueGo.Dtos.ReviewDtos.PublicReviews;
using VenueGo.Dtos.ReviewDtos.WriteReview;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Services.Auth;
using VenueGo.Services.Reviews;

namespace VenueGo.Controllers.Api
{
    // ════════════════════════════════════════════════════════════════
    //  顧客端評論 API（取代 Controllers/CReviewController.cs 的 Razor 版）
    //  規格：《API規格_CReview.md》。這支的每個 Action 標題都寫了對應的規格編號。
    //
    //  ── 10/9 改版：現場評論限會員 ──────────────────────────────────
    //  運動中心改成只限會員入場，現場評論也改成「登入後從會員中心進入」：
    //    1. 現場評論的網址從 visit/{QRToken} 改成 visit/{id}（id ＝ ReviewPerVisitId），
    //       而且只有憑證上的會員本人打得開。
    //    2. 所有會員才能用的端點都指定「用 JWT 驗證、而且要有 Member 角色」。
    //       預設的驗證方式是 Cookie（後台員工用），不指定的話前台帶 JWT 也會被當成沒登入。
    //    3. 「能不能評論」的判斷搬到 IReviewEligibilityService（Services/Reviews/）。
    //    4. 舊的 Razor 版 CReviewController 退役，這支是唯一的一份。
    //
    //  ── 10/10 Service 抽取 ────────────────────────────────────────
    //  規則全部搬到 Services/Reviews/：
    //    - IPublicReviewQueryService：評論專區（上架條件、篩選、排序、星等分布）
    //    - IMemberReviewService：撰寫頁設定、送出、我的評論、已讀、切換公開、表態、員工預覽
    //    - IReviewEligibilityService：能不能評論（10/9 已抽出，上面兩個 Service 會呼叫它）
    //  這支 Controller 只剩 HTTP 的事：路由、[Authorize]、檢查請求本體、
    //  把 Service 的結果翻成狀態碼＋ApiResult（第一區）。不再直接用 DbContext。
    //  原本給 API 用的 ViewModel 搬到 Dtos/ReviewDtos/，改成 Dto 結尾。
    //
    //  ── 為什麼叫 CReviewApiController，不沿用 CReviewController ─────────
    //  當初 Razor 版還在，兩個同名的 Controller 在預設路由下會衝突，所以換了名字。
    //  Razor 版刪掉之後想改回來，只要改類別名稱，網址不受影響（網址是下面 [Route] 決定的）。
    //
    //  ── 為什麼繼承 ControllerBase，不是 Controller ────────────────────
    //  Controller = ControllerBase + View 相關的功能（View()、TempData、ViewBag⋯）。
    //  這支只回 JSON，用不到那些。繼承 ControllerBase，想不小心寫出 return View() 也寫不出來。
    //
    //  ── [ApiController]（9/29 起照組裡規定掛上）─────────────────────
    //  掛了之後，模型驗證失敗會在「進 Action 之前」就被框架攔下（回 400）。
    //  回應的格式由 Program.cs 掛的 ApiResponses.InvalidModelState 統一成 ApiResult（規格 0-5），
    //  所以這支不再自己檢查 ModelState。
    //  ⚠️ 附帶的順序變化：表單有錯「而且」憑證也過期時，以前先回 410，現在先回 400。
    //     前台兩種都會顯示訊息，不影響使用。
    //
    //  ── 位置（9/29）──────────────────────────────────────────────
    //  組裡規定 API 放 Controllers/Api/；回應格式放 Dtos/Reviews/；轉換放 Mappers/ReviewMapper。
    //
    //  ── 跟 Razor 版的邏輯關係 ────────────────────────────────────────
    //  上架條件、組卡片、寫入的規則照搬自已退役的 CReviewController。
    //  改的是「怎麼回應」：轉址＋TempData → 狀態碼＋JSON；以及 10/9 的限會員改版（見上方）。
    // ════════════════════════════════════════════════════════════════

    [ApiController]
    [Route("api/reviews")]
    // 原本：[AutoValidateAntiforgeryToken]   // 所有 POST 都要驗防偽 token（GET 不驗）。token 放在 RequestVerificationToken 標頭，見規格 0-6
    // 10/10 拿掉防偽 token 驗證（RPV 驗收時送出評論出現「頁面已經過期」）：
    //   1. 原因：防偽 token 會綁定「拿 token 當下的登入者」。前台拿 token 的 /api/antiforgery/token 走預設的 Cookie 驗證，
    //      認不出 JWT，所以拿到的是「未登入者」的 token；送評論時這支用 JWT 認出是會員，兩邊對不上 → 400。
    //      沙盒已重現：錯誤訊息是「token was meant for a different claims-based user than the current user」。
    //   2. 為什麼可以拿掉：防偽 token 防的是 CSRF——別的網站利用「瀏覽器會自動帶 Cookie」冒用你的身分送請求。
    //      這支的端點全部用 JWT（放在 Authorization 標頭，瀏覽器不會自動帶），別的網站拿不到、也送不出來，CSRF 不成立。
    //      組裡其他 Controllers/Api 底下用 JWT 的 API 也都沒有掛防偽 token。
    //   3. 員工預覽（preview）是 Cookie 驗證，但它是 GET（只讀），本來就不驗防偽 token。
    //   ⚠️ 之後如果在這支新增「用 Cookie 驗證的 POST」，那一支要自己掛 [ValidateAntiForgeryToken]。
    // 10/10 新增：不讓瀏覽器快取這支 API 的任何回應（RPV 驗收時「超過可以評論的時間」卡住的修正）。
    //   Chrome 會把沒有快取標頭的 410 Gone 當成「永久有效」存起來，之後同一個網址都直接拿快取，不再問伺服器：
    //   時光機撥回來、開新分頁、重新整理都一樣卡在 410。沙盒用 Chromium 重現過，加上這行之後每次都會重新詢問。
    //   評論 API 回的都是會員個人的資料，本來就不該被快取（no-store）。
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public sealed class CReviewApiController(
        IPublicReviewQueryService publicReviews,
        IMemberReviewService memberReviews,
        ICurrentUserService currentUserService) : ControllerBase
    {
        private readonly IPublicReviewQueryService _publicReviews = publicReviews;
        private readonly IMemberReviewService _memberReviews = memberReviews;
        private readonly ICurrentUserService _currentUser = currentUserService;

        // ════════════════════════════════════════════════════════
        //  第一區：Service 的結果 → HTTP 回應（規格 0-3）
        //  每種結果對應哪個狀態碼、哪個 ErrorCode，只寫在這一個 switch 裡。
        // ════════════════════════════════════════════════════════

        private ObjectResult Fail(int status, string errorCode, string message, object? data = null)
            => StatusCode(status, new ApiResult<object?>
            {
                Success = false,
                Message = message,
                ErrorCode = errorCode,
                Data = data
            });

        /// <summary>失敗的結果種類 → 狀態碼＋ErrorCode＋訊息。</summary>
        private ObjectResult FailFor(MemberReviewOutcome outcome) => outcome switch
        {
            MemberReviewOutcome.NotFound        => Fail(404, "NotFound", "查無指定評論"),
            MemberReviewOutcome.Expired         => Fail(410, "Expired", "超過可以評論的時間囉，下次請早"),
            MemberReviewOutcome.AlreadyReviewed => Fail(409, "AlreadyReviewed", "這張憑證已經評論過了"),
            MemberReviewOutcome.NoReply         => Fail(409, "NoReply", "館方還沒有回覆，無法表態"),
            MemberReviewOutcome.AlreadyRated    => Fail(409, "AlreadyRated", "你已經表態過了"),
            MemberReviewOutcome.SpamMarked      => Fail(409, "SpamMarked", "這則評論已下架，無法切換公開狀態"),
            // Ok 不是失敗；其他值代表有人新增了結果種類卻忘了處理。
            // 直接丟例外讓它在開發時就被看到，不要默默回一個不對的狀態碼。
            _ => throw new InvalidOperationException($"未處理的結果種類：{outcome}")
        };

        /// <summary>成功 → 200＋ApiResult&lt;T&gt;；失敗 → <see cref="FailFor"/>。</summary>
        private IActionResult ToResponse<T>(MemberReviewResult<T> result)
            => result.Outcome == MemberReviewOutcome.Ok
                ? Ok(ApiResult<T>.Ok(result.Data!))
                : FailFor(result.Outcome);

        /// <summary>送出評論用：成功只回訊息，沒有 Data。</summary>
        private IActionResult ToCreatedResponse(MemberReviewOutcome outcome)
            => outcome == MemberReviewOutcome.Ok
                ? Ok(ApiResult.Ok("評論已送出"))
                : FailFor(outcome);

        // 9/29：原本的 ValidationFailed()（把 ModelState 整理成規格 0-5）搬到 Helpers/ApiResponses.InvalidModelState，
        //       由 [ApiController] 自動呼叫，全站 API 共用。

        private static bool IsValidSatisfaction(SatisfactionRequestDto? body)
            => body?.Satisfaction is byte s && s <= 2;

        private ObjectResult InvalidSatisfaction() => Fail(400, "InvalidSatisfaction", "輸入異常，請重試");

        // 10/10：第二區（資格判定）、第三區（組回應、寫入）的私有方法都搬到 Services/Reviews/，
        //        區塊編號保留不動，方便對照舊版。

        // ════════════════════════════════════════════════════════
        //  第四區：Action
        //  現場評論與預約評論分成兩個 Action，而不是共用一個 {kind}：
        //    1. 兩邊的識別碼意思不同（現場是憑證 Id、預約是訂單 Id），分開寫比較不會搞混。
        //    2. 路由寫 {id:int}／{orderId:int}，不是整數的網址直接 404，不必進 Action 再檢查。
        //  兩邊共用的邏輯都在 Services/Reviews/ 裡（10/10 起），
        //  Action 本身只剩「接參數 → 呼叫 Service → 用第一區翻成回應」。
        //
        //  ── 會員限定的端點都掛這一行 ──────────────────────────────
        //  [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        //  AuthenticationSchemes：用 JWT 驗證（預設是 Cookie，那是後台員工用的）。
        //  Roles：JWT 裡要有 Member 角色。目前前台登入發的 JWT 一定有 Member，
        //         寫出來是為了讓人一看就知道「這是會員專用」，以後有別種身分拿到 JWT 也不會意外放行。
        // ════════════════════════════════════════════════════════

        // ── 2-1 評論專區 ─────────────────────────────────────

        /// <summary>查詢評論專區</summary>
        /// <remarks>
        /// 回傳公開的評論卡片、星等分布、平均分數，以及篩選選項與預設值。**不需要登入。**
        ///
        /// ### 公開條件
        /// 只有現場評論、沒有被標為垃圾、顧客選擇公開，而且「已回覆，或建立超過緩衝天數」的評論才會出現。
        ///
        /// ### 注意
        /// - 星等分布用「套用星等篩選之前」的結果計算，所以切換星等時分布圖不會變。
        /// - 還沒有分頁，`page`、`totalPages` 目前固定是 1。
        /// - 不認得的 `range`、`sort` 一律改用預設值，不回錯誤。
        /// </remarks>
        /// <param name="sportTypeId">運動類型的 Id；不給代表全部</param>
        /// <param name="range" example="year">時間範圍：`7days`、`30days`、`year`、`all`；預設 `year`</param>
        /// <param name="star" example="5">只看幾顆星（1～5）；不給代表全部</param>
        /// <param name="hasContentOnly">只看有文字內容的評論</param>
        /// <param name="sort" example="newest">排序：`newest`、`highest`、`lowest`；預設 `newest`</param>
        /// <response code="200">成功</response>
        [HttpGet("")]
        [ProducesResponseType<ApiResult<PublicReviewListDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPublicReviews(
            int? sportTypeId, string? range, int? star, bool hasContentOnly = false, string? sort = null)
        {
            var data = await _publicReviews.GetPublicReviewsAsync(sportTypeId, range, star, hasContentOnly, sort);
            return Ok(ApiResult<PublicReviewListDto>.Ok(data));
        }

        // ── 2-2 撰寫頁設定 ───────────────────────────────────

        /// <summary>取得現場評論表單</summary>
        /// <remarks>
        /// 回傳撰寫頁的設定（要顯示哪些欄位、預設值、字數上限），同時判定這張憑證現在能不能評論。**需要會員本人登入。**
        ///
        /// ### 注意
        /// - 憑證不是本人的時候回 404，不會透露「這張憑證存在」。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <response code="200">可以評論，Data 是表單設定</response>
        /// <response code="404">查無評論資格：憑證不存在或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">這張憑證已經評論過了。ErrorCode：`AlreadyReviewed`</response>
        /// <response code="410">超過可以評論的時間。ErrorCode：`Expired`</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        /// <response code="403">登入的不是會員。ErrorCode：`Forbidden`</response>
        [HttpGet("visit/{id:int}/form")]
        [ProducesResponseType<ApiResult<WriteFormDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status410Gone)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> GetVisitForm(int id)
            => ToResponse(await _memberReviews.GetVisitFormAsync(id, _currentUser.UserId));

        /// <summary>取得預約評論表單</summary>
        /// <remarks>
        /// 回傳撰寫頁的設定，同時判定這筆訂單現在能不能評論。**需要會員本人登入。**
        ///
        /// ### 注意
        /// - 預約評論一律不公開、不匿名，表單不會顯示這兩個選項。
        /// - 訂單有付款紀錄、但還沒有評論憑證時，會當場補建憑證（付款系統可能漏了呼叫工廠）。
        /// - 訂單不是本人的時候回 404，不會透露「這筆訂單存在」。
        /// </remarks>
        /// <param name="orderId" example="9007">訂單 Id</param>
        /// <response code="200">可以評論，Data 是表單設定</response>
        /// <response code="404">查無評論資格：憑證不存在或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">這張憑證已經評論過了。ErrorCode：`AlreadyReviewed`</response>
        /// <response code="410">超過可以評論的時間。ErrorCode：`Expired`</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        /// <response code="403">登入的不是會員（例如員工帳號）。ErrorCode：`Forbidden`</response>
        [HttpGet("booking/{orderId:int}/form")]
        [ProducesResponseType<ApiResult<WriteFormDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status410Gone)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> GetBookingForm(int orderId)
            => ToResponse(await _memberReviews.GetBookingFormAsync(orderId, _currentUser.UserId));

        // ── 2-3 送出評論 ─────────────────────────────────────

        /// <summary>送出現場評論</summary>
        /// <remarks>
        /// **需要會員本人登入。**
        ///
        /// ### 注意
        /// - 資格會在送出時重新判定一次：表單可能在畫面上停了好幾天。
        /// - 存好之後會建立預審紀錄；預審失敗不影響這次送出。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <param name="vm">評論內容：星等（必填）、文字、提及標籤、匿名、公開</param>
        /// <response code="200">送出成功，Message 是「評論已送出」</response>
        /// <response code="400">欄位驗證失敗，Data.errors 是各欄位的錯誤訊息。ErrorCode：`ValidationFailed`</response>
        /// <response code="404">查無評論資格：憑證不存在或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">這張憑證已經評論過了。ErrorCode：`AlreadyReviewed`</response>
        /// <response code="410">超過可以評論的時間。ErrorCode：`Expired`</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        /// <response code="403">登入的不是會員。ErrorCode：`Forbidden`</response>
        [HttpPost("visit/{id:int}")]
        [ProducesResponseType<ApiResult>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status410Gone)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> CreateVisit(int id, [FromBody] ReviewCreateForVisitDto? vm)
        {
            // 欄位驗證（星等必填、字數上限⋯）已經由 [ApiController] 在進來之前做完了；這裡只剩「整個本體沒送」
            if (vm == null) return BadRequest(ApiResponses.EmptyBody());

            // 資格會在 Service 裡重驗一次：表單可能停在畫面上好幾天，也可能有人繞過畫面直接送請求
            return ToCreatedResponse(await _memberReviews.CreateVisitReviewAsync(id, _currentUser.UserId, vm));
        }

        /// <summary>送出預約評論</summary>
        /// <remarks>
        /// **需要會員本人登入。**
        ///
        /// ### 注意
        /// - 預約評論一律不公開、不匿名：前端送來的這兩個值會被忽略。
        /// - 資格會在送出時重新判定一次。
        /// - 存好之後會建立預審紀錄；預審失敗不影響這次送出。
        /// </remarks>
        /// <param name="orderId" example="9007">訂單 Id</param>
        /// <param name="vm">評論內容：星等（必填）、文字</param>
        /// <response code="200">送出成功，Message 是「評論已送出」</response>
        /// <response code="400">欄位驗證失敗，Data.errors 是各欄位的錯誤訊息。ErrorCode：`ValidationFailed`</response>
        /// <response code="404">查無評論資格：憑證不存在或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">這張憑證已經評論過了。ErrorCode：`AlreadyReviewed`</response>
        /// <response code="410">超過可以評論的時間。ErrorCode：`Expired`</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        /// <response code="403">登入的不是會員（例如員工帳號）。ErrorCode：`Forbidden`</response>
        [HttpPost("booking/{orderId:int}")]
        [ProducesResponseType<ApiResult>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status410Gone)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> CreateBooking(int orderId, [FromBody] ReviewCreateForBookingDto? vm)
        {
            if (vm == null) return BadRequest(ApiResponses.EmptyBody());

            // 預約評論一律不公開、不匿名：DTO 上沒有這兩個欄位，Service 固定存成 false
            return ToCreatedResponse(await _memberReviews.CreateBookingReviewAsync(orderId, _currentUser.UserId, vm));
        }

        // ── 2-4 我的評論（純讀取，不寫入已讀）─────────────────

        /// <summary>查看我的現場評論</summary>
        /// <remarks>
        /// 回傳自己寫的評論、館方回覆與目前的狀態。**需要會員本人登入**；不是本人回 404。
        ///
        /// **純讀取**：不會記錄「顧客已看過回覆」，那是另一支 `POST .../reply-viewed`。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <response code="200">成功</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        [HttpGet("visit/{id:int}")]
        [ProducesResponseType<ApiResult<MyReviewDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> GetVisitReview(int id)
            => ToResponse(await _memberReviews.GetVisitReviewAsync(id, _currentUser.UserId));

        /// <summary>查看我的預約評論</summary>
        /// <remarks>
        /// 回傳自己寫的評論、館方回覆與目前的狀態。**需要會員本人登入**；不是本人回 404。
        ///
        /// **純讀取**：不會記錄「顧客已看過回覆」，那是另一支 `POST .../reply-viewed`。
        /// </remarks>
        /// <param name="orderId" example="9002">訂單 Id</param>
        /// <response code="200">成功</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        [HttpGet("booking/{orderId:int}")]
        [ProducesResponseType<ApiResult<MyReviewDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> GetBookingReview(int orderId)
            => ToResponse(await _memberReviews.GetBookingReviewAsync(orderId, _currentUser.UserId));

        // ── 2-8 員工預覽 ─────────────────────────────────────

        /// <summary>員工預覽評論</summary>
        /// <remarks>
        /// 員工從館方清單打開「以顧客視角預覽」。回傳的格式跟「查看我的評論」一模一樣，前端用同一個畫面顯示。**只有後台角色可以用。**
        ///
        /// ### 為什麼另開一支
        /// 1. 「查看我的評論」要會員本人登入，員工打不開顧客的評論。
        /// 2. 只有後台角色進得來，所以網址可以直接用 ReviewId。「不用 ReviewId 當網址參數」防的是沒有權限的人從 1 掃到 100，這裡進得來的人本來就看得到全部。
        ///
        /// **純讀取**：不寫入「顧客已看過回覆」，員工看過不等於顧客看過。
        /// </remarks>
        /// <param name="reviewId">評論 Id</param>
        /// <response code="200">成功</response>
        /// <response code="401">沒有登入。ErrorCode：`NotLoggedIn`</response>
        /// <response code="403">不是後台角色。ErrorCode：`Forbidden`</response>
        /// <response code="404">查無評論。ErrorCode：`NotFound`</response>
        [HttpGet("preview/{reviewId:int}")]
        [ProducesResponseType<ApiResult<MyReviewDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [Authorize(Roles = RoleNames.BackOffice)]
        public async Task<IActionResult> GetPreview(int reviewId)
            => ToResponse(await _memberReviews.GetPreviewAsync(reviewId));

        // ── 2-5 記錄已讀 ─────────────────────────────────────

        /// <summary>記錄已讀館方回覆</summary>
        /// <remarks>
        /// 顧客看到館方回覆之後由前端呼叫，用來統計回覆的已讀率。**需要會員本人登入。**
        ///
        /// ### 注意
        /// - 只在「有回覆而且還沒記錄過」時寫入；重複呼叫不會蓋掉第一次的時間。
        /// - 還沒有回覆時不寫入，Data.viewed 是 `false`。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <response code="200">成功，Data.viewed 表示目前是否已記錄為已讀</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        [HttpPost("visit/{id:int}/reply-viewed")]
        [ProducesResponseType<ApiResult<ReplyViewedDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> MarkVisitReplyViewed(int id)
            => ToResponse(await _memberReviews.MarkVisitReplyViewedAsync(id, _currentUser.UserId));

        /// <summary>記錄已讀館方回覆</summary>
        /// <remarks>
        /// 預約評論版本，規則同現場評論。**需要會員本人登入。**
        /// </remarks>
        /// <param name="orderId" example="9002">訂單 Id</param>
        /// <response code="200">成功，Data.viewed 表示目前是否已記錄為已讀</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        [HttpPost("booking/{orderId:int}/reply-viewed")]
        [ProducesResponseType<ApiResult<ReplyViewedDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> MarkBookingReplyViewed(int orderId)
            => ToResponse(await _memberReviews.MarkBookingReplyViewedAsync(orderId, _currentUser.UserId));

        // ── 2-6 切換公開（只有現場評論）───────────────────────

        /// <summary>切換評論公開</summary>
        /// <remarks>
        /// 顧客把自己的現場評論改成公開或不公開。只有現場評論有這支（預約評論一律不公開）。**需要會員本人登入。**
        ///
        /// 被館方標為垃圾的評論強制不公開，不能切換。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <param name="body">`{ "isPublic": true }`：要變成的狀態，不是「反過來」</param>
        /// <response code="200">成功，Data.isPublic 是切換後的狀態</response>
        /// <response code="400">沒有指定 isPublic。ErrorCode：`ValidationFailed`</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">評論已被下架，不能切換。ErrorCode：`SpamMarked`</response>
        [HttpPost("visit/{id:int}/visibility")]
        [ProducesResponseType<ApiResult<VisibilityResultDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> SetVisibility(int id, [FromBody] VisibilityRequestDto? body)
        {
            if (body?.IsPublic is not bool isPublic)
                return Fail(400, "ValidationFailed", "請指定要公開或不公開",
                            new { errors = new Dictionary<string, string[]> { ["isPublic"] = new[] { "請指定要公開或不公開" } } });

            // 被標記垃圾的評論強制不公開。Razor 版是安靜地轉回頁面；API 版明確回錯誤碼（SpamMarked）
            return ToResponse(await _memberReviews.SetVisitVisibilityAsync(id, _currentUser.UserId, isPublic));
        }

        // ── 2-7 對回覆表態 ───────────────────────────────────

        /// <summary>對館方回覆表態</summary>
        /// <remarks>
        /// 顧客對館方回覆表示滿不滿意。每則評論只能表態一次。**需要會員本人登入。**
        ///
        /// `satisfaction`：`0` 不滿意、`1` 普通、`2` 滿意。
        /// </remarks>
        /// <param name="id" example="1">現場評論憑證 Id（ReviewPerVisitId）</param>
        /// <param name="body">`{ "satisfaction": 2 }`</param>
        /// <response code="200">成功，Data.satisfaction 是存下來的值</response>
        /// <response code="400">satisfaction 不是 0、1、2。ErrorCode：`InvalidSatisfaction`</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">館方還沒回覆（`NoReply`），或已經表態過（`AlreadyRated`）</response>
        [HttpPost("visit/{id:int}/satisfaction")]
        [ProducesResponseType<ApiResult<SatisfactionResultDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> SetVisitSatisfaction(int id, [FromBody] SatisfactionRequestDto? body)
        {
            if (!IsValidSatisfaction(body)) return InvalidSatisfaction();
            return ToResponse(await _memberReviews.SetVisitSatisfactionAsync(id, _currentUser.UserId, body!.Satisfaction!.Value));
        }

        /// <summary>對館方回覆表態</summary>
        /// <remarks>
        /// 預約評論版本，規則同現場評論。**需要會員本人登入。**
        /// </remarks>
        /// <param name="orderId" example="9002">訂單 Id</param>
        /// <param name="body">`{ "satisfaction": 2 }`</param>
        /// <response code="200">成功，Data.satisfaction 是存下來的值</response>
        /// <response code="400">satisfaction 不是 0、1、2。ErrorCode：`InvalidSatisfaction`</response>
        /// <response code="404">查無評論：憑證不存在、還沒評論過，或不是本人。ErrorCode：`NotFound`</response>
        /// <response code="409">館方還沒回覆（`NoReply`），或已經表態過（`AlreadyRated`）</response>
        [HttpPost("booking/{orderId:int}/satisfaction")]
        [ProducesResponseType<ApiResult<SatisfactionResultDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ApiResult>(StatusCodes.Status409Conflict)]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
        public async Task<IActionResult> SetBookingSatisfaction(int orderId, [FromBody] SatisfactionRequestDto? body)
        {
            if (!IsValidSatisfaction(body)) return InvalidSatisfaction();
            return ToResponse(await _memberReviews.SetBookingSatisfactionAsync(orderId, _currentUser.UserId, body!.Satisfaction!.Value));
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  跟 Razor 版不同的地方（規則面）
    //
    //  1. NormalizeRange 補上 ReviewRange.Year。
    //     Razor 版的 switch 只列了 Week／Month／All，「一年內」要靠「不認得 → 回預設」才選得到。
    //     剛好預設就是 Year，所以畫面上看不出來；但只要哪天預設改成別的，
    //     「一年內」這個選項就會選了等於沒選。這裡補上，讓每個選項都被明確認得。
    //     （Razor 版要不要一起補，由你決定；它之後會被刪掉。）
    //
    //  2. 「我的評論」的 GET 不再寫入 ReplyViewedAt，改成另外一支 POST（規格 2-5）。
    //
    //  3. 失敗不再「轉址 + TempData」，改回狀態碼 + ApiResult（規格 0-3）。
    //
    //  4. 欄位驗證失敗先於資格判定（因為 [ApiController]），見檔頭說明。
    //     10/10 起「整個本體沒送」也一樣先回 400，再判定資格（以前是先判定資格）。
    //     兩者都是「請求本身有問題」，放在同一個順序比較一致。
    // ════════════════════════════════════════════════════════════════
}
