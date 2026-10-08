namespace VenueGo.Models.Constants
{
    /// <summary>
    /// 使用者狀態（Users.Status）的允許值。
    /// <para>
    /// 此欄位在資料庫是 nvarchar(20) 而非 tinyint，因此用字串常數而不是列舉，
    /// 目的是讓程式中不再出現 "Active" 這類魔術字串，改字時只需改這裡一處。
    /// </para>
    /// <para>
    /// 【與會員管理子系統的約定】新增狀態值時務必先同步此檔案，
    /// 不要在各自的 Controller 內直接比對字串。
    /// </para>
    /// </summary>
    public static class UserStatuses
    {
        /// <summary>正常：可登入、可預約。資料庫預設值。</summary>
        public const string Active = "Active";

        /// <summary>停權：由管理員停用，不可預約。</summary>
        public const string Suspended = "Suspended";

        /// <summary>已註銷：會員自行申請刪除帳號。</summary>
        public const string Inactive = "Inactive";

        /// <summary>
        /// 白名單，供寫入前驗證用，避免任意字串被存進 Users.Status。
        /// [補修正] 原本 MemberController.UpdateMemberStatus 直接把前端送來的字串寫入，
        /// 完全沒有驗證，這裡補上跟 EmployeeStatuses.AllowedStatuses 一致的防護。
        /// </summary>
        public static readonly IReadOnlySet<string> AllowedStatuses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Active, Suspended, Inactive };
    }
}