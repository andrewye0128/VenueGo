using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.MemberViewModels
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

        // [NEW-方案B] 若此員工目前為離職/留停狀態，顯示其離職前的角色快照（逗號分隔字串），
        // 僅供管理員在畫面上參考，方便復職時手動重新勾選 SelectedRoleIds，不參與表單送出的驗證與繫結。
        [Display(Name = "離職前角色")]
        public string? PreviousRoleNames { get; set; }
    }
}