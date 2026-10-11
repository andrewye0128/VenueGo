namespace VenueGo.Models.Constants
{
    /// <summary>
    /// 稽核紀錄的動作代碼，寫入 AuditLogs.Action（nvarchar 100）。
    /// <para>
    /// 【為何要統一】五個人各自寫字串的話，同一個動作會出現
    /// "CancelReservation"、"cancel_reservation"、"取消預約" 三種寫法，
    /// 之後要查「所有取消操作」就得把三種都列進 WHERE 條件。
    /// </para>
    /// <para>
    /// 【命名規則】動詞 + 主體，PascalCase，一律英文。
    /// 中文描述在畫面顯示時才組出來，不存進資料庫。
    /// </para>
    /// </summary>
    public static class AuditActions
    {
        /// <summary>建立預約（後台代客建立或會員自助預約）。</summary>
        public const string CreateReservation = "CreateReservation";

        /// <summary>將訂單標記為已付款。</summary>
        public const string MarkOrderAsPaid = "MarkOrderAsPaid";

        /// <summary>取消預約（會員因素退訂）。</summary>
        public const string CancelReservation = "CancelReservation";

        /// <summary>作廢預約（管理員建錯單或測試資料）。</summary>
        public const string VoidReservation = "VoidReservation";

        /// <summary>場館取消預約（場地維護等館方因素）。</summary>
        public const string CancelReservationByVenue = "CancelReservationByVenue";

        /// <summary>編輯預約（修改日期、場地或時段）。期末功能。</summary>
        public const string UpdateReservation = "UpdateReservation";

        // ── 以下為 Setting（角色 / 員工管理）相關動作 ──
        // [重構搬移] 原本直接寫死在 SettingController 裡的字串，
        // 搬進這裡統一管理；字串值刻意維持原本的拼法，
        // 避免跟資料庫裡既有的 AuditLogs 歷史紀錄對不起來。

        /// <summary>建立新角色。</summary>
        public const string CreateRole = "CreateRole";

        /// <summary>更新角色的權限指派（EditRole）。</summary>
        public const string UpdateRolePermissions = "UpdateRolePermissions";

        /// <summary>建立員工帳號。</summary>
        public const string CreateUser = "CreateUser";

        /// <summary>編輯員工帳號資料（含角色異動、在職狀態變更）。</summary>
        public const string EditUser = "EditUser";

        /// <summary>
        /// 變更員工在職狀態（獨立於 EditUser 的快速切換端點）。
        /// 目前對應的 Controller Action 已確認沒有任何畫面在呼叫（死代碼待清除），
        /// 這裡先保留常數，供未來若要重新啟用該功能時使用。
        /// </summary>
        public const string UpdateEmployeeStatus = "UpdateEmployeeStatus";

        /// <summary>使用者修改自己的個人資料。</summary>
        public const string UpdateProfile = "UpdateProfile";

        /// <summary>將既有會員升格為員工。</summary>
        public const string ConvertToEmployee = "ConvertToEmployee";

        // ── 以下為 MemberController（會員管理）相關動作 ──
        // [重構搬移] 字串值維持原本在 MemberController 裡的拼法，相容既有 AuditLogs 資料。

        /// <summary>變更會員狀態（Active/Suspended/Inactive）。</summary>
        public const string UpdateMemberStatus = "UpdateMemberStatus";

        /// <summary>管理員重置會員密碼為預設密碼。</summary>
        public const string ResetMemberPassword = "ResetMemberPassword";

        /// <summary>管理員解除會員帳號的登入失敗鎖定。</summary>
        public const string UnlockMemberAccount = "UnlockMemberAccount";

        /// <summary>
        /// 匯出會員名單為 CSV。
        /// [補修正] 原本這個動作完全沒有寫入稽核紀錄——匯出全部會員的 Email、電話、
        /// 累計消費等個資卻無法追查是誰在什麼時候匯出的，這次重構順便補上。
        /// </summary>
        public const string ExportMembersToCsv = "ExportMembersToCsv";
    }

    /// <summary>
    /// 稽核紀錄的 EntityType 值，填資料表名稱。
    /// </summary>
    public static class AuditEntityTypes
    {
        public const string Reservations = "Reservations";
        public const string Orders = "Orders";
        public const string Payments = "Payments";
        public const string Users = "Users";
        public const string Venues = "Venues";

        // ── 以下為 Setting（角色 / 員工管理）相關實體類型 ──
        // [重構搬移][注意] 這裡刻意用單數（Role / Employee / User），
        // 跟上面 Users（複數）是兩組不同情境下各自長出來的命名，不一致但不能統一，
        // 因為 SettingController 既有的 AuditLogs 資料已經是用單數寫入，
        // 改成複數會讓 GetLastRemovedRolesSnapshotAsync 查不到舊紀錄。

        /// <summary>角色（對應 SettingController 的 Roles/EditRole/CreateRole）。</summary>
        public const string Role = "Role";

        /// <summary>員工（對應 SettingController 的 EditUser/UpdateEmployeeStatus）。</summary>
        public const string Employee = "Employee";

        /// <summary>使用者（對應 SettingController 的 CreateUser）。</summary>
        public const string User = "User";

        
    }
}