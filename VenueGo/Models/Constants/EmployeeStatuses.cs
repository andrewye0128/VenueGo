namespace VenueGo.Models.Constants
{
    /// <summary>
    /// 員工在職狀態（Employees.Status）的允許值。
    /// <para>
    /// 跟 <see cref="UserStatuses"/> 是兩個獨立的概念：UserStatuses 管的是「這個人還算不算
    /// 系統的正常使用者」，EmployeeStatuses 管的是「這個人現在算不算在職的員工」。
    /// 一個人可能同時是 UserStatuses.Active（會員帳號正常）又是 EmployeeStatuses.Resigned
    /// （員工身份已離職）——離職員工通常還保留一般會員身份，只是拿掉後台角色。
    /// </para>
    /// <para>
    /// 【為何獨立出來】原本這組白名單是寫死在 SettingController 裡的
    /// private HashSet，只有那一支 Controller 看得到、用得到。
    /// 抽成常數類別後，之後任何地方（例如登入檢查、報表、其他管理頁）
    /// 要判斷「這個員工是不是在職」，都能直接引用這裡，不用各自重寫一次字串比對。
    /// </para>
    /// </summary>
    public static class EmployeeStatuses
    {
        /// <summary>在職。</summary>
        public const string Active = "Active";

        /// <summary>離職。</summary>
        public const string Resigned = "Resigned";

        /// <summary>留職停薪。</summary>
        public const string OnLeave = "OnLeave";

        /// <summary>
        /// 白名單，供寫入前驗證用，避免任意字串被存進 Employees.Status。
        /// 用 OrdinalIgnoreCase 比對，跟原本 SettingController 的寫法保持一致。
        /// </summary>
        public static readonly IReadOnlySet<string> AllowedStatuses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Active, Resigned, OnLeave };
    }
}