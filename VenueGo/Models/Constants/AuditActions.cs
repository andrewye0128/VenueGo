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
    }
}