namespace VenueGo.ViewModels
{
    public class UserListViewModel
    {
        // 搜尋與篩選條件
        public string? Keyword { get; set; }
        public int? SelectedRoleId { get; set; }
        public string? SelectedStatus { get; set; }

        // 新增：目前選取的帳號類型篩選 (all, employee, member)
        public string? SelectedUserType { get; set; } = "all";

        // 選單資料
        public List<RoleOptionDto> AvailableRoles { get; set; } = new();

        // 列表結果
        public List<UserListItemDto> Users { get; set; } = new();
    }

    public class UserListItemDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string EmployeeNo { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        // 新增：是否為員工 (用來判斷顯示「編輯」或「轉任員工」按鈕)
        public bool IsEmployee { get; set; }
    }
}
