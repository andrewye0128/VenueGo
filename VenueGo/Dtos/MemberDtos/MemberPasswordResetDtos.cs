
using System.ComponentModel.DataAnnotations;

namespace VenueGo.Dtos.MemberDtos
{
    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "請輸入 Email")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;
    }

    public class ValidateResetTokenRequestDto
    {
        [Required(ErrorMessage = "Email 不可為空")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "重設 Token 不可為空")]
        public string Token { get; set; } = string.Empty;
    }

    public class ResetMemberPasswordRequestDto
    {
        [Required(ErrorMessage = "Email 不可為空")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "重設 Token 不可為空")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入新密碼")]
        [StringLength(
            100,
            MinimumLength = 8,
            ErrorMessage = "密碼長度至少需為 8 個字")]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "密碼必須包含至少一個大寫英文字母、一個小寫英文字母與一個數字")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "請再次輸入新密碼")]
        [Compare("Password", ErrorMessage = "兩次輸入的密碼不一致")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
