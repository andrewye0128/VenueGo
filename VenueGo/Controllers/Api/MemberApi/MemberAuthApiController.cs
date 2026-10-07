using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VenueGo.Data;
using VenueGo.Services.Auth;
using VenueGo.ViewModels.MemberViewModels;
using VenueGo.ViewModels;
using VenueGo.Dtos.MemberDtos;
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
                 ApiResultVM.Fail(
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
                    ApiResultVM.Fail(
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
                    ApiResultVM.Fail(
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
    }
}