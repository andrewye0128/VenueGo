namespace VenueGo.Services.Auth;

public interface IAuthenticationService
{
    /// <summary>後台員工登入。除了帳密核心驗證，還會檢查 Employee 身份、在職狀態、後台角色。</summary>
    Task<LoginResult> LoginAsync(string email, string password, string ipAddress);

    /// <summary>
    /// 前台會員登入。只做帳密核心驗證（帳號存在、未鎖定、密碼正確、Users.Status 為 Active），
    /// 不要求 Employee 身份、不要求任何後台角色——這正是會員跟員工登入唯一的差異。
    /// 成功時回傳的 LoginResult.Roles 固定為 [RoleNames.Member]，EmployeeId/EmployeeNo 為 null。
    /// </summary>
    Task<LoginResult> LoginAsMemberAsync(string email, string password, string ipAddress);
}