namespace VenueGo.Services.Members
{
    /// <summary>
    /// 帶資料的 Service 回傳結果。
    /// <para>
    /// 【為何需要】<see cref="ServiceResult"/> 只能說「成功／失敗」，但有些操作成功後，
    /// Controller 還需要一筆資料才能組出畫面訊息（例如停權成功後要顯示「已將會員 王小明 …」，
    /// 會員姓名是 Service 查出來的）。若讓 Controller 為了取姓名再查一次資料庫，
    /// 等於把查詢又寫回 Controller，違背拆分的初衷。
    /// </para>
    /// <para>
    /// 用法跟 <see cref="ServiceResult"/> 完全一樣：依 <see cref="Outcome"/> 分流，
    /// 只有 <see cref="OperationOutcome.Success"/> 時 <see cref="Data"/> 才有值。
    /// 這裡用「包一個 ServiceResult」而不是繼承，是為了不去改動既有的 ServiceResult。
    /// </para>
    /// </summary>
    public sealed class ServiceResult<T>
    {
        private readonly ServiceResult _inner;

        private ServiceResult(ServiceResult inner, T? data)
        {
            _inner = inner;
            Data = data;
        }

        public OperationOutcome Outcome => _inner.Outcome;

        public bool IsSuccess => _inner.IsSuccess;

        /// <summary>驗證失敗時的欄位錯誤清單，Key 為空字串代表一般錯誤。</summary>
        public List<(string Key, string Message)> Errors => _inner.Errors;

        /// <summary>系統錯誤時的訊息。</summary>
        public string? ErrorMessage => _inner.ErrorMessage;

        /// <summary>成功時附帶的資料；失敗時為預設值。</summary>
        public T? Data { get; }

        public static ServiceResult<T> Success(T data) => new(ServiceResult.Success(), data);

        public static ServiceResult<T> NotFound() => new(ServiceResult.NotFound(), default);

        public static ServiceResult<T> ValidationFailed(string key, string message) =>
            new(ServiceResult.ValidationFailed(key, message), default);

        public static ServiceResult<T> ValidationFailed(IEnumerable<(string Key, string Message)> errors) =>
            new(ServiceResult.ValidationFailed(errors), default);

        public static ServiceResult<T> Error(string message) => new(ServiceResult.Error(message), default);
    }
}