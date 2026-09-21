using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    public class EditUserViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "員工編號")]
        public string EmployeeNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入 Email")]
        [EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入姓名")]
        [StringLength(50, ErrorMessage = "姓名長度不能超過 50 個字")]
        [Display(Name = "姓名")]
        public string Name { get; set; } = string.Empty;

        [Phone(ErrorMessage = "請輸入有效的電話號碼")]
        [Display(Name = "電話")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "請輸入職稱")]
        [StringLength(50, ErrorMessage = "職稱長度不能超過 50 個字")]
        [Display(Name = "職稱")]
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "請選擇帳號狀態")]
        [Display(Name = "帳號狀態")]
        public string Status { get; set; } = "Active";

        [Display(Name = "已指派角色")]
        public List<int> SelectedRoleIds { get; set; } = new();

        // 系統中所有可供選擇的後台角色 (已排除 Member / Customer)
        public List<RoleOptionDto> AvailableRoles { get; set; } = new();
    }
}