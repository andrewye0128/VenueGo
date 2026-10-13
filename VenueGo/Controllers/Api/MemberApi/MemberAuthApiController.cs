using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.MemberViewModels;
using VenueGo.Dtos.MemberDtos;
using VenueGo.Dtos;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Helpers;
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

        public MemberAuthApiController(
            IAuthenticationService authenticationService,
            IJwtService jwtService,
            dbVenueContext db,
            ICurrentUserService currentUser)
        {
            _authenticationService = authenticationService;
            _jwtService = jwtService;
            _db = db;
            _currentUser = currentUser;
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

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register(
    [FromBody] RegisterMemberRequestDto model)
        {
            // 1. 檢查生日
            if (!model.Birth.HasValue)
            {
                return BadRequest(
                    ApiResult.Fail("請選擇出生日期。", "ValidationFailed"));
            }

            var birth = model.Birth.Value;
            var today = DateOnly.FromDateTime(DateTime.Today);

            if (birth > today)
            {
                return BadRequest(
                    ApiResult.Fail("生日不可晚於今天。", "ValidationFailed"));
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
                return Conflict(
                    ApiResult.Fail(
                        "此 Email 已經註冊，請使用其他 Email。",
                        "EmailAlreadyExists"));
            }

            // 4. 取得系統中的會員角色
            var memberRole = await _db.Roles
                .FirstOrDefaultAsync(r =>
                    r.RoleName == RoleNames.Member && r.Status);

            if (memberRole == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult.Fail(
                        "系統尚未設定可用的會員角色，請聯繫管理員。",
                        "MemberRoleNotConfigured"));
            }

            // 5. 建立會員資料
            // 注意：這裡先使用 DateTime.UtcNow 作為暫時時間來源。
            // 若專案有統一的台灣時間服務，下一步應改用既有服務。
            var now = DateTime.UtcNow;

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
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                // 可能是兩個請求同時註冊相同 Email，
                // 因此再確認是否已存在。
                var duplicateEmail = await _db.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Email == normalizedEmail);

                if (duplicateEmail)
                {
                    return Conflict(
                        ApiResult.Fail(
                            "此 Email 已經註冊，請使用其他 Email。",
                            "EmailAlreadyExists"));
                }

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    ApiResult.Fail(
                        "建立會員帳號失敗，請稍後再試。",
                        "RegistrationFailed"));
            }

            // 7. 註冊成功，不在這裡自動登入
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResult<object>.Ok(new
                {
                    message = "註冊成功，請使用您的 Email 與密碼登入。"
                }));
        }
    }
}