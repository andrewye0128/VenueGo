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


        /// <summary>
        /// 登入失敗時的錯誤代碼（給程式判斷，對應前台 ErrorCodes.js）。
        /// 例如前台可能想對「帳號鎖定中」顯示倒數計時 UI，對「帳號已停權」顯示聯繫客服的提示，
        /// 這種需要不同畫面處理的情境，不該透過比對中文訊息字串來判斷。
        /// 只有 Success 為 false 時才會有值。
        /// </summary>
        public string? ErrorCode { get; init; }

        /// <summary>
        /// 使用者 ID。
        /// </summary>
        public int? UserId { get; init; }

        /// <summary>
        /// 使用者姓名。
        /// </summary>
        public string? UserName { get; init; }

        /// <summary>
        /// 使用者 Email。
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// 員工 ID。
        /// </summary>
        public int? EmployeeId { get; init; }

        /// <summary>
        /// 員工編號。
        /// </summary>
        public string? EmployeeNo { get; init; }

        /// <summary>
        /// 使用者的後台角色。
        /// </summary>
        public List<string> Roles { get; init; } = new();
    }
}