using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Dtos;
using VenueGo.Services.Auth;
using VenueGo.Services.Members;
using VenueGo.ViewModels.MemberViewModels;

namespace VenueGo.Controllers.Api.MemberApi
{
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

        [HttpPut]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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

            var result = await _profileService.UpdateProfileAsync(
                userId.Value,
                model
            );

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
                    return BadRequest(
                        ApiResult.Fail(
                            result.ErrorMessage ?? "資料驗證失敗。",
                            "ValidationFailed"
                        )
                    );

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
