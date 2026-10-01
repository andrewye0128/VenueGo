namespace VenueGo.Services.Auth
{
    /// <summary>
    /// 登入驗證的結果。
    /// AuthenticationService 負責產生，
    /// AccountController 再依結果決定要怎麼回應使用者。
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// 是否登入成功。
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// 登入失敗時提供給前端的訊息。
        /// </summary>
        public string? ErrorMessage { get; init; }
    }
}