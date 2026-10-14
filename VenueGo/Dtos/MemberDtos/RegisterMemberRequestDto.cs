using System.ComponentModel.DataAnnotations;

namespace VenueGo.Dtos.MemberDtos
{
    public class RegisterMemberRequestDto
    {
        [Required(ErrorMessage = "請輸入姓名")]
        [StringLength(50, ErrorMessage = "姓名長度不能超過 50 個字")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入 Email")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        [StringLength(254, ErrorMessage = "Email 長度不能超過 254 個字")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼")]
        [StringLength(
            100,
            MinimumLength = 8,
            ErrorMessage = "密碼長度必須為 8～100 個字元"
        )]
        [RegularExpression(
            @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "密碼必須包含大寫、小寫英文字母及數字"
        )]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "請再次輸入密碼")]
        [Compare(
            nameof(Password),
            ErrorMessage = "兩次輸入的密碼不一致"
        )]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入手機號碼")]
        [RegularExpression(
            @"^09\d{8}$",
            ErrorMessage = "請輸入 09 開頭的 10 碼手機號碼"
        )]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "請選擇出生日期")]
        public DateOnly? Birth { get; set; }

        [RegularExpression(
            @"^/[A-Za-z0-9.+-]{7}$",
            ErrorMessage = "載具格式不正確，請輸入斜線開頭的 7 碼載具"
        )]
        [StringLength(20, ErrorMessage = "載具號碼長度不能超過 20 個字")]
        public string? CarrierNo { get; set; }
    }
}
