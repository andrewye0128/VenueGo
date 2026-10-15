using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Dtos;
using VenueGo.Dtos.MemberDtos;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Services;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.MemberViewModels;
using Microsoft.AspNetCore.RateLimiting;
namespace VenueGo.Controllers.Api
{
    [ApiController]
    [Route("api/member/auth")]
    public class MemberAuthApiController : ControllerBase
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly IJwtService _jwtService;
        private readonly dbVenueContext _db;
        private readonly ICurrentUserService _currentUser;
        private readonly IPasswordResetService _passwordResetService;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ITimeService _time;
        private readonly ILogger<MemberAuthApiController> _logger;
        private readonly IConfiguration _configuration;

        public MemberAuthApiController(
            IAuthenticationService authenticationService,
            IJwtService jwtService,
            dbVenueContext db,
            ICurrentUserService currentUser,
            IPasswordResetService passwordResetService,
            IEmailService emailService,
            IWebHostEnvironment environment,
            ITimeService time,
            IConfiguration configuration,
            ILogger<MemberAuthApiController> logger)
        {
            _authenticationService = authenticationService;
            _jwtService = jwtService;
            _db = db;
            _currentUser = currentUser;
            _passwordResetService = passwordResetService;
            _emailService = emailService;
            _environment = environment;
            _time = time;
            _logger = logger;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "127.0.0.1";

            var loginResult =
                await _authenticationService.LoginAsMemberAsync(
                    model.Email,
                    model.Password,
                    ipAddress
                );

            if (!loginResult.Success)
            {
                // 直接透傳 AuthenticationService 判斷好的 ErrorCode，
                // 前台才能依不同原因（帳號鎖定/已停權/密碼錯誤）顯示不同畫面
                return Unauthorized(
                 ApiResult.Fail(
                     loginResult.ErrorMessage ?? "登入失敗",
                     loginResult.ErrorCode ?? "LoginFailed"
                 )
               );
            }

            var token =
                _jwtService.GenerateToken(loginResult);
            var response = new MemberLoginResponseDto
            {
                Token = token,
                UserId = loginResult.UserId,
                Name = loginResult.UserName,
                Email = loginResult.Email
            };

            return Ok(ApiResult<MemberLoginResponseDto>.Ok(response));
        }
        /*為什麼 /me 特別重要？
       因為它可以直接證明：
       會員登入
          ↓
       取得 JWT
          ↓
       前端帶 Authorization: Bearer JWT
          ↓
       JwtBearer 驗證
          ↓
       HttpContext.User
          ↓
       取得 UserId / Name / Email / Role
       而且我們特別寫：
       AuthenticationSchemes =
       JwtBearerDefaults.AuthenticationScheme
       所以這支 API 明確要求 JWT。
       不是 Cookie。*/
        [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {      
            var role =
                User.FindFirstValue(ClaimTypes.Role);
            var userId = _currentUser.UserId;

            if (userId == null)
            {
                return Unauthorized(
                    ApiResult.Fail(
                        "無效的登入資訊，請重新登入。",
                        "InvalidToken"
                    )
                );
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return Unauthorized(
                    ApiResult.Fail(
                        "找不到對應的會員資料，請重新登入。",
                        "InvalidToken"
                    )
                );
            }
            var response = new MemberMeResponseDto
            {
                UserId = user.UserId.ToString(),
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Birth = user.Birth.ToString("yyyy-MM-dd"),
                CarrierNo=user.CarrierNo,
                Role = role
            };

            return Ok(ApiResult<MemberMeResponseDto>.Ok(response));
        }
        // 會員註冊：POST /api/member/auth/register
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
    [FromBody] RegisterMemberRequestDto model)
        {
            // DTO 上的驗證屬性（必填、格式、兩次密碼一致…）由 [ApiController] 先檢查，
            // 失敗時 Program.cs 掛的 ApiResponses.InvalidModelState 會直接回 400，不會進到這裡。
            // 下面是屬性驗證不到的規則。錯誤一律用 ApiResponses.ValidationFailed，
            // 前台 http.js 的 error.fieldErrors 才拿得到，頁面可以放回對應的欄位底下。

            // 1. 檢查生日
            if (!model.Birth.HasValue)
            {
                //return BadRequest(
                //    ApiResult.Fail("請選擇出生日期。", "ValidationFailed"));
                return BadRequest(ApiResponses.ValidationFailed(new[] { (nameof(model.Birth), "請選擇出生日期。") }));
            }

            var birth = model.Birth.Value;
            var now = _time.Now;
            var today = DateOnly.FromDateTime(now);

            if (birth > today)
            {
                //return BadRequest(
                //    ApiResult.Fail("生日不可晚於今天。", "ValidationFailed"));
                return BadRequest(ApiResponses.ValidationFailed(new[] { (nameof(model.Birth), "生日不可早於 1900 年。") }));
            }

            if (birth < new DateOnly(1900, 1, 1))
            {
                return BadRequest(
                    ApiResult.Fail("生日日期不正確。", "ValidationFailed"));
            }

            // 2. Email 統一格式
            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            // 3. 檢查 Email 是否已註冊
            var emailExists = await _db.Users
                .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

            if (emailExists)
            {
                //return Conflict(
                //    ApiResult.Fail(
                //        "此 Email 已經註冊，請使用其他 Email。",
                //        "EmailAlreadyExists"));
                return EmailAlreadyExists();
            }

            // 4. 取得系統中的會員角色
            var memberRole = await _db.Roles
                .FirstOrDefaultAsync(r =>
                    r.RoleName == RoleNames.Member && r.Status);

            if (memberRole == null)
            {
                _logger.LogError("Member registration failed: role {RoleName} is missing or disabled", RoleNames.Member);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult.Fail(
                        "系統尚未設定可用的會員角色，請聯繫管理員。",
                        "MemberRoleNotConfigured"));
            }

            // 5. 建立會員資料
            // 注意：這裡先使用 DateTime.UtcNow 作為暫時時間來源。
            // 若專案有統一的台灣時間服務，下一步應改用既有服務。
           // var now = DateTime.UtcNow;

            var user = new User
            {
                Name = model.Name.Trim(),
                Email = normalizedEmail,
                PasswordHash = PasswordHelper.HashPassword(model.Password),
                Phone = model.Phone.Trim(),
                Birth = birth,
                CarrierNo = string.IsNullOrWhiteSpace(model.CarrierNo)
                    ? null
                    : model.CarrierNo.Trim().ToUpperInvariant(),

                Status = UserStatuses.Active,

                CumulativeConsumption = 0,
                CumulativeVisitTime = 0,
                FailedLoginCount = 0,
                NoShowCount = 0,

                LockedUntil = null,
                LastLoginAt = null,

                CreatedAt = now,
                UpdatedAt = now
            };

            // 6. 使用交易，確保會員與會員角色一起建立
            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.UserId,
                    RoleId = memberRole.RoleId,
                    AssignedBy = null,
                    AssignedAt = now
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();

                // 失敗的 User 還留在這個 DbContext 的追蹤清單裡，先清掉，下面的查詢才不會受影響
                _db.ChangeTracker.Clear();

                // 可能是兩個請求同時註冊相同 Email，
                // 因此再確認是否已存在。
                var duplicateEmail = await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Email == normalizedEmail);

                if (duplicateEmail)
                {
                    //return Conflict(
                    //    ApiResult.Fail(
                    //        "此 Email 已經註冊，請使用其他 Email。",
                    //        "EmailAlreadyExists"));
                    return EmailAlreadyExists();
                }
                // 其他資料庫錯誤要留下紀錄，不然只會看到前台的「建立會員帳號失敗」，無從查起
                _logger.LogError(ex, "Member registration failed for {Email}", normalizedEmail);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult.Fail(
                        "建立會員帳號失敗，請稍後再試。",
                        "RegistrationFailed"));
            }

            // 7. 註冊成功，不在這裡自動登入
            return Ok(ApiResult.Ok("註冊成功，請使用您的 Email 與密碼登入。"));
            //return StatusCode(
            //    StatusCodes.Status201Created,
            //    ApiResult<object>.Ok(new
            //    {
            //        message = "註冊成功，請使用您的 Email 與密碼登入。"
            //    }));
        }

        // 忘記密碼：申請重設 Token
        
        // 忘記密碼：申請重設 Token
        [AllowAnonymous]
        [EnableRateLimiting("ForgotPassword")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequestDto model)
        {
            var ipAddress =
                HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "127.0.0.1";

            var rawToken = await _passwordResetService.CreateResetTokenAsync(
                model.Email,
                ipAddress
            );

            // 不論 Email 是否存在，都回傳相同的訊息。
            const string message =
                "如果此 Email 已註冊且帳號有效，請查看您的電子郵件以繼續重設密碼。";

            // 僅在帳號有效且成功產生 Token 時寄信。
            if (rawToken != null)
            {
                //try
                //{
                //    // 從設定檔讀取前端網址。
                //    var frontendBaseUrl =
                //        _configuration["Frontend:BaseUrl"];

                //    if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                //    {
                //        throw new InvalidOperationException(
                //            "尚未設定 Frontend:BaseUrl。");
                //    }

                //    // 請確認 /reset-password 與 Vue Router 的路由一致。
                //    var resetUrl =
                //        $"{frontendBaseUrl.TrimEnd('/')}/reset-password" +
                //        $"?email={Uri.EscapeDataString(model.Email.Trim())}" +
                //        $"&token={Uri.EscapeDataString(rawToken)}";

                //    var bodyHtml = $"""
                //<h2>VenueGo 重設密碼</h2>
                //<p>您好，</p>
                //<p>我們收到重設您 VenueGo 帳號密碼的要求。</p>
                //<p>
                //    <a href="{System.Net.WebUtility.HtmlEncode(resetUrl)}">
                //        點此重設密碼
                //    </a>
                //</p>
                //<p>此連結將於 30 分鐘後失效。</p>
                //<p>如果您沒有提出此要求，請忽略這封郵件。</p>
                //""";

                //    await _emailService.SendEmailAsync(
                //        model.Email.Trim(),
                //        "VenueGo 密碼重設",
                //        bodyHtml
                //    );
                //}
                //catch (Exception ex)
                //{
                //    // 不記錄 Token 或完整重設網址。
                //    _logger.LogError(
                //        ex,
                //        "Failed to send password reset email.");
                //}


                try
                {
                    // 從設定檔讀取前端網址。
                    var frontendBaseUrl = _configuration["Frontend:BaseUrl"];

                    if (string.IsNullOrWhiteSpace(frontendBaseUrl))
                    {
                        throw new InvalidOperationException(
                            "尚未設定 Frontend:BaseUrl。");
                    }

                    // 建立重設密碼連結。
                    var resetUrl =
                        $"{frontendBaseUrl.TrimEnd('/')}/reset-password" +
                        $"?email={Uri.EscapeDataString(model.Email.Trim())}" +
                        $"&token={Uri.EscapeDataString(rawToken)}";

                    var bodyHtml = $"""
        <h2>VenueGo 重設密碼</h2>
        <p>您好，</p>
        <p>我們收到重設您 VenueGo 帳號密碼的要求。</p>
        <p>
            <a href="{System.Net.WebUtility.HtmlEncode(resetUrl)}">
                點此重設密碼
            </a>
        </p>
        <p>此連結將於 30 分鐘後失效。</p>
        <p>如果您沒有提出此要求，請忽略這封郵件。</p>
        """;

                    // 先寄信。
                    await _emailService.SendEmailAsync(
                        model.Email.Trim(),
                        "VenueGo 密碼重設",
                        bodyHtml
                    );

                    // 寄信成功後，才讓其他尚未使用的 Token 失效。
                    var confirmed =
                        await _passwordResetService.ConfirmResetEmailSentAsync(
                            model.Email,
                            rawToken
                        );

                    if (!confirmed)
                    {
                        _logger.LogWarning(
                            "Password reset email sent, but token confirmation failed.");
                    }
                }
                catch (Exception ex)
                {
                    // 寄信或後續處理失敗時，嘗試取消這次建立的 Token。
                    _logger.LogError(
                        ex,
                        "Failed to complete password reset email request.");

                    try
                    {
                        await _passwordResetService.CancelResetTokenAsync(
                            model.Email,
                            rawToken
                        );
                    }
                    catch (Exception cancelEx)
                    {
                        _logger.LogError(
                            cancelEx,
                            "Failed to cancel password reset token.");
                    }
                }

            }

            // 僅供本機開發測試；正式環境不回傳 Token。
            var data = _environment.IsDevelopment() && rawToken != null
                ? new { resetToken = rawToken }
                : new { resetToken = (string?)null };

            return Ok(ApiResult<object>.Ok(data, message));
        }
        //[AllowAnonymous]
        //[HttpPost("forgot-password")]
        //public async Task<IActionResult> ForgotPassword(
        //    [FromBody] ForgotPasswordRequestDto model)
        //{
        //    var ipAddress =
        //        HttpContext.Connection.RemoteIpAddress?.ToString()
        //        ?? "127.0.0.1";

        //    var rawToken = await _passwordResetService.CreateResetTokenAsync(
        //        model.Email,
        //        ipAddress
        //    );

        //    // 不論 Email 是否存在，都回傳相同的訊息，避免洩漏帳號資訊。
        //    const string message =
        //        "如果此 Email 已註冊且帳號有效，您將可以繼續重設密碼流程。";

        //    // 僅供本機開發測試；正式環境不回傳重設 Token。
        //    var data = _environment.IsDevelopment() && rawToken != null
        //        ? new { resetToken = rawToken }
        //        : new { resetToken = (string?)null };

        //    return Ok(ApiResult<object>.Ok(data, message));
        //}


        // 驗證重設 Token 是否有效
        [AllowAnonymous]
        [HttpPost("validate-reset-token")]
        public async Task<IActionResult> ValidateResetToken(
            [FromBody] ValidateResetTokenRequestDto model)
        {
            var isValid = await _passwordResetService.ValidateResetTokenAsync(
                model.Email,
                model.Token
            );

            if (!isValid)
            {
                return BadRequest(ApiResult.Fail(
                    "重設密碼連結已失效，請重新申請。",
                    "InvalidResetToken"
                ));
            }

            return Ok(ApiResult.Ok("重設連結有效。"));
        }


        // 重設會員密碼
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetMemberPasswordRequestDto model)
        {
            var success = await _passwordResetService.ResetPasswordAsync(
                model.Email,
                model.Token,
                model.Password
            );

            if (!success)
            {
                return BadRequest(ApiResult.Fail(
                    "重設密碼連結已失效，請重新申請。",
                    "InvalidResetToken"
                ));
            }

            return Ok(ApiResult.Ok("密碼重設成功，請使用新密碼登入。"));
        }

        // 409：這個 Email 已經註冊。
        // errorCode 給程式判斷（EmailAlreadyExists），同時用跟欄位驗證一樣的 data.errors 形狀，
        // 前台可以直接把訊息放回 Email 欄位底下。
        private IActionResult EmailAlreadyExists()
        {
            const string message = "此 Email 已經註冊，請使用其他 Email。";

            return Conflict(new ApiResult<object>
            {
                Success = false,
                Message = message,
                ErrorCode = "EmailAlreadyExists",
                Data = new { errors = new Dictionary<string, string[]> { ["email"] = new[] { message } } }
            });
        }
    }
}