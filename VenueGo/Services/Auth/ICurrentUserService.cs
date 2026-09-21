namespace VenueGo.Services.Auth
{
    /// <summary>
    /// 目前登入者的資訊。
    /// <para>
    /// 【為何要包一層】登入資訊存在加密 Cookie 的 Claims 裡，
    /// 取用方式是 User.FindFirstValue(ClaimTypes.NameIdentifier) 再 int.Parse。
    /// 若每個需要「誰在操作」的地方都寫一次，
    /// 會散落十幾處相同的解析與 null 處理；而且只要有一處忘記處理未登入，
    /// 就會在執行時拋 FormatException。
    /// </para>
    /// <para>
    /// 【為何不從表單傳】管理員 Id 絕不可由前端傳入。
    /// 隱藏欄位任何人都能改，改成別人的 Id 之後，
    /// 稽核紀錄就會把操作記到無辜的同事身上。
    /// Cookie 裡的 Claims 是加密簽章過的，使用者改不了。
    /// </para>
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// 目前登入者的 Users.UserId。未登入時為 null。
        /// <para>
        /// 注意這是 UserId 而非 EmployeeId，與
        /// Reservations.CreatedBy、AuditLogs.UserId 等欄位參照的對象一致。
        /// </para>
        /// </summary>
        int? UserId { get; }

        /// <summary>目前登入者的姓名。未登入時為 null。</summary>
        string? UserName { get; }

        /// <summary>目前登入者的員工代碼。未登入時為 null。</summary>
        string? EmployeeNo { get; }

        /// <summary>是否已登入。</summary>
        bool IsAuthenticated { get; }
    }
}