using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    // 角色清單列表項目
    public class RoleListItemViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Status { get; set; }
        public int UserCount { get; set; }
        public List<string> PermissionNames { get; set; } = new();
    }

    // 角色新增 / 編輯表單
    public class RoleEditViewModel
    {
        public int RoleId { get; set; }

        [Required(ErrorMessage = "請輸入角色名稱")]
        [StringLength(50, ErrorMessage = "名稱長度不能超過 50 個字")]
        public string RoleName { get; set; } = string.Empty;

        public string? Description { get; set; }
        public bool Status { get; set; } = true;

        // 已勾選的 PermissionId 列表
        public List<int> SelectedPermissionIds { get; set; } = new();

        // 可供選擇的權限列表 (供 View 繪製 Checkbox)
        public List<PermissionOptionDto> AvailablePermissions { get; set; } = new();
    }

    public class PermissionOptionDto
    {
        public int PermissionId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public string PermissionName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Status { get; set; }
    }

    // 新增角色的 View Model
    public class RoleCreateViewModel
    {
        [Required(ErrorMessage = "請輸入角色名稱")]
        [StringLength(50, ErrorMessage = "角色名稱長度不能超過 50 個字")]
        [Display(Name = "角色名稱")]
        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "角色描述")]
        public string? Description { get; set; }

        [Display(Name = "狀態")]
        public bool Status { get; set; } = true;

        [Display(Name = "勾選權限")]
        public List<int> SelectedPermissionIds { get; set; } = new();

        public List<PermissionOptionDto> AvailablePermissions { get; set; } = new();
    }
}
