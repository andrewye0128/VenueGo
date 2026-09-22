using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.MemberViewModels
{
    public class UserProfileViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "員工編號 / 帳號")]
        public string? EmployeeNo { get; set; }

        [Required(ErrorMessage = "請輸入姓名")]
        [StringLength(50, ErrorMessage = "姓名長度不能超過 50 個字")]
        [Display(Name = "姓名")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入 Email")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        [Display(Name = "電子郵件")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入連絡電話")]
        [Phone(ErrorMessage = "電話格式不正確")]
        [Display(Name = "連絡電話")]
        public string Phone { get; set; } = string.Empty;

        [Display(Name = "職稱")]
        public string? JobTitle { get; set; }

        [Display(Name = "角色權限")]
        public List<string> Roles { get; set; } = new();

        // 變更密碼專用欄位 (選填，若有輸入才驗證)
        [DataType(DataType.Password)]
        [Display(Name = "舊密碼")]
        public string? CurrentPassword { get; set; }

        [StringLength(100, MinimumLength = 8, ErrorMessage = "新密碼長度至少需為 8 個字")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "密碼必須包含至少一個大寫英文字母、一個小寫英文字母與一個數字")]
        [DataType(DataType.Password)]
        [Display(Name = "新密碼")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "兩次輸入的新密碼不一致")]
        [Display(Name = "確認新密碼")]
        public string? ConfirmNewPassword { get; set; }
    }
}