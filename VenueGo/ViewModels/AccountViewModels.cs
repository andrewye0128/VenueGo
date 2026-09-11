using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    public class RegisterUserViewModel
    {
        [Required(ErrorMessage = "請輸入姓名")]
        [StringLength(50, ErrorMessage = "姓名長度不能超過 50 個字")]
        [Display(Name = "姓名")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入 Email")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼長度至少需為 6 個字")]
        [DataType(DataType.Password)]
        [Display(Name = "密碼")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "請再次確認密碼")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "兩次輸入的密碼不一致")]
        [Display(Name = "確認密碼")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入連絡電話")]
        [Phone(ErrorMessage = "電話格式不正確")]
        [Display(Name = "連絡電話")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "請選擇出生日期")]
        [Display(Name = "出生日期")]
        public DateOnly Birth { get; set; }

        // ================= 員工專屬擴充欄位 =================

        [Display(Name = "員工代碼")]
        public string? EmployeeNo { get; set; }

        [StringLength(50, ErrorMessage = "職稱長度不能超過 50 個字")]
        [Display(Name = "職稱")]
        public string? JobTitle { get; set; } // 移除 [Required]，改由 Controller 根據 Action 邏輯做動態驗證

        // ================= 指派角色權限 =================

        [Display(Name = "指派角色")]
        public List<int> SelectedRoleIds { get; set; } = new();

        public List<RoleOptionDto> AvailableRoles { get; set; } = new();
    }

    public class RoleOptionDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool Status { get; set; }
    }
}