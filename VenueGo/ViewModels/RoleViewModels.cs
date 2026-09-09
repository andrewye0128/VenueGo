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
}
