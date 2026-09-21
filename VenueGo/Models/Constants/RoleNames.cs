namespace VenueGo.Models.Constants
{
    /// <summary>
    /// 角色名稱常數，對應 Roles.RoleName 欄位的值。
    /// <para>
    /// 【為何不用列舉】[Authorize(Roles = "...")] 這個屬性只接受編譯期常數字串，
    /// 無法傳入列舉值。若各 Controller 直接打字串，打錯一個字母不會編譯失敗，
    /// 只會默默把所有人都擋在門外，而且很難查。
    /// </para>
    /// </summary>
    public static class RoleNames
    {
        /// <summary>一般會員。前台使用，不可進入後台。</summary>
        public const string Member = "Member";

        /// <summary>員工。</summary>
        public const string Staff = "Staff";

        /// <summary>場館營運管理者。</summary>
        public const string Manager = "Manager";

        /// <summary>系統管理員。</summary>
        public const string Admin = "Admin";

        /// <summary>
        /// 所有可進入後台的角色，供 [Authorize(Roles = RoleNames.BackOffice)] 使用。
        /// <para>
        /// 期末開放會員前台後，會員也會拿到驗證 Cookie。
        /// 屆時只寫 [Authorize] 的頁面會變成「會員也能進後台」，
        /// 因此現在就把角色限制寫清楚，期末不必回頭逐一檢查。
        /// </para>
        /// </summary>
        public const string BackOffice = $"{Staff},{Manager},{Admin}";


        /// <summary>場館營運管理者與系統管理員 </summary>
        public const string ManagerOrAdmin = $"{Manager},{Admin}";

        /// <summary>員工與場館營運管理者</summary>
        public const string StaffOrManager = $"{Staff},{Manager}";
    }
}