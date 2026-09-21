namespace VenueGo.ViewModels
{
    /// <summary>
    /// AJAX 端點的統一回傳格式。只給回傳 JSON 的 Action 用，回傳 View 的不要包。
    /// 送到前端後屬性名會變成小寫開頭：success / message / errorCode / data。
    /// </summary>
    public class ApiResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }     // 給人看
        public string? ErrorCode { get; init; }   // 給程式判斷，前端不要比對中文字

        public static ApiResult Ok(string? message = null)
            => new() { Success = true, Message = message };

        public static ApiResult Fail(string message, string? errorCode = null)
            => new() { Success = false, Message = message, ErrorCode = errorCode };
    }

    public class ApiResult<T> : ApiResult
    {
        public T? Data { get; init; }

        public static ApiResult<T> Ok(T data, string? message = null)
            => new() { Success = true, Data = data, Message = message };
    }
}
