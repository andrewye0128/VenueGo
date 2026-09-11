using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    public class ConvertEmployeeViewModel
    {
        public int UserId { get; set; }

        [Display(Name = "會員姓名")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入員工編號")]
        [Display(Name = "員工編號")]
        public string EmployeeNo { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "職稱長度不能超過 50 個字")]
        [Display(Name = "職稱")]
        public string? JobTitle { get; set; }

        [Display(Name = "指派角色")]
        public List<int> SelectedRoleIds { get; set; } = new();

        public List<RoleOptionDto> AvailableRoles { get; set; } = new();
    }
}