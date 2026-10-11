namespace VenueGo.Services.Members
{
    /// <summary>
    /// <see cref="ServiceResult"/> 的結果種類。
    /// <para>
    /// 【為何要這四種，不是單純的 bool】
    /// Controller 原本針對不同失敗情況，要回傳不一樣的結果：
    /// 找不到資料回 404（NotFound），欄位驗證失敗要把錯誤塞回 ModelState
    /// 並回到同一頁表單（ValidationFailed），資料庫寫入出錯要顯示系統錯誤訊息（Error）。
    /// 如果只回傳 bool，Controller 沒辦法分辨這三種情況，等於把判斷邏輯又推回 Controller，
    /// 違背了把邏輯收進 Service 的初衷。
    /// </para>
    /// </summary>
    public enum OperationOutcome
    {
        /// <summary>操作成功。</summary>
        Success,

        /// <summary>找不到要操作的資料（例如 RoleId/UserId 不存在）。</summary>
        NotFound,

        /// <summary>輸入資料沒有通過驗證（例如角色名稱重複、違反業務規則）。</summary>
        ValidationFailed,

        /// <summary>執行過程中發生非預期的錯誤（例如資料庫寫入失敗）。</summary>
        Error
    }

    /// <summary>
    /// Service 方法的統一回傳結果。
    /// <para>
    /// 【Controller 端的慣用寫法】
    /// <code>
    /// var result = await _roleService.UpdateRoleAsync(model, currentUserId);
    /// switch (result.Outcome)
    /// {
    ///     case OperationOutcome.Success:
    ///         TempData["SuccessMessage"] = "...";
    ///         return RedirectToAction(nameof(Roles));
    ///     case OperationOutcome.NotFound:
    ///         return NotFound();
    ///     case OperationOutcome.ValidationFailed:
    ///         foreach (var (key, msg) in result.Errors)
    ///             ModelState.AddModelError(key, msg);
    ///         model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
    ///         return View(model);
    ///     default: // Error
    ///         ModelState.AddModelError("", result.ErrorMessage ?? "操作失敗，請稍後再試。");
    ///         model.AvailablePermissions = await _roleService.GetAvailablePermissionsAsync();
    ///         return View(model);
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public class ServiceResult
    {
        public OperationOutcome Outcome { get; private init; }

        /// <summary>
        /// 驗證失敗時的欄位錯誤清單。Key 對應 ModelState 的欄位名稱
        /// （例如 "RoleName"），空字串代表「不綁定特定欄位」的一般錯誤，
        /// 跟原本 Controller 裡 <c>ModelState.AddModelError("", ...)</c> 的用法一致。
        /// 一個 Key 可以出現多次（例如同一欄位有多條規則都沒過），Controller 端用 foreach 全部加進 ModelState。
        /// </summary>
        public List<(string Key, string Message)> Errors { get; private init; } = new();

        /// <summary>系統錯誤時的訊息（Outcome 為 Error 才會有值）。</summary>
        public string? ErrorMessage { get; private init; }

        public bool IsSuccess => Outcome == OperationOutcome.Success;

        public static ServiceResult Success() => new() { Outcome = OperationOutcome.Success };

        public static ServiceResult NotFound() => new() { Outcome = OperationOutcome.NotFound };

        public static ServiceResult ValidationFailed(IEnumerable<(string Key, string Message)> errors) =>
            new() { Outcome = OperationOutcome.ValidationFailed, Errors = errors.ToList() };

        /// <summary>只有單一筆驗證錯誤時的簡便寫法。</summary>
        public static ServiceResult ValidationFailed(string key, string message) =>
            ValidationFailed(new[] { (key, message) });

        public static ServiceResult Error(string message) =>
            new() { Outcome = OperationOutcome.Error, ErrorMessage = message };
    }
}