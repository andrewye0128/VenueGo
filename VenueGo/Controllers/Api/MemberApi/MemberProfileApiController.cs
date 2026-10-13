using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Dtos.MemberDtos;
using VenueGo.Helpers;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;

namespace VenueGo.Controllers.Api.MemberApi
{
    /// <summary>
    /// 前台會員的個人資料。userId 一律來自 Token（ICurrentUserService），不接受前端傳入。
    /// 讀取請用 GET /api/member/auth/me；這裡只負責修改。
    /// </summary>
    [ApiController]
    [Route("api/member/profile")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MemberProfileApiController : ControllerBase
    {
        private readonly IUserProfileService _profileService;
        private readonly ICurrentUserService _currentUser;

        public MemberProfileApiController(
            IUserProfileService profileService,
            ICurrentUserService currentUser)
        {
            _profileService = profileService;
            _currentUser = currentUser;
        }

        /// <summary>
        /// 修改姓名、手機、載具，有填新密碼時一併改密碼。
        /// 輸入格式錯誤（必填、長度、手機格式）由 [ApiController] 自動回 400，
        /// 格式是 ApiResult（見 Program.cs 掛的 ApiResponses.InvalidModelState），
        /// 所以這裡不用再檢查 ModelState。
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateMemberProfileRequestDto? request)
        {
            // 本體整個沒送時，參數要宣告成可為 null，框架才會把 null 交進來（見 ApiResponses.EmptyBody 的說明）
            if (request == null)
            {
                return BadRequest(ApiResponses.EmptyBody());
            }

            if (_currentUser.UserId is not int userId)
            {
                return Unauthorized(
                    ApiResult.Fail(
                        "無效的登入資訊，請重新登入。",
                        "InvalidToken"
                    )
                );
            }

            var result = await _profileService.UpdateMemberProfileAsync(userId, request);

            switch (result.Outcome)
            {
                case OperationOutcome.Success:
                    return Ok(
                        ApiResult.Ok("個人資料更新成功")
                    );

                case OperationOutcome.NotFound:
                    return NotFound(
                        ApiResult.Fail(
                            "找不到會員資料。",
                            "NotFound"
                        )
                    );

                case OperationOutcome.ValidationFailed:
                    // 注意：Service 的驗證訊息放在 result.Errors，不是 result.ErrorMessage
                    // （ErrorMessage 只有 Error 結果才有值）。原本讀 ErrorMessage，
                    // 所以「舊密碼輸入錯誤」這類訊息永遠顯示不出來。
                    return BadRequest(ApiResponses.ValidationFailed(result.Errors));

                default:
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        ApiResult.Fail(
                            result.ErrorMessage ?? "更新個人資料時發生錯誤。",
                            "UpdateProfileFailed"
                        )
                    );
            }
        }
    }
}