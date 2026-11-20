using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using VenueGo.Dtos;

namespace VenueGo.Helpers
{
    // ════════════════════════════════════════════════════════════════
    //  API 統一例外處理（「總機」）
    //
    //  API（網址 /api 開頭）發生沒預料到的錯誤（例外）時，由這裡統一接住：
    //    1. 把完整的錯誤記進 log，附上錯誤代碼（TraceIdentifier）
    //    2. 回給前台 500＋ApiResult，跟其他 API 同一種格式：
    //       { success: false, errorCode: "ServerError",
    //         message: "系統發生錯誤，請稍後再試（錯誤代碼：xxx）",
    //         data: { traceId: "xxx", detail: { type, message } } }
    //       detail 只有開發環境才有；正式環境只回通用的訊息，不讓外人看到錯誤細節
    //
    //  只看守 /api：後台頁面（Razor）照舊，開發時顯示錯誤細節頁、上線時顯示 /Home/Error。
    //  Controller 自己寫的 try/catch 照舊，這裡只接「沒人接」的錯誤。
    //
    //  Program.cs 的設定（AddExceptionHandler、AddProblemDetails、UseWhen）見那邊的註解。
    //  前台：http.js 收到 ApiResult 會直接顯示 message；errorCode 常數在 src/constants/errorCodes.js
    //
    //  客人在處理完成前關掉網頁：ASP.NET Core 的 UseExceptionHandler 自己會處理
    //  （只記一般的 Debug 訊息、不回內容），不會交給這裡，所以這裡不用另外判斷
    // ════════════════════════════════════════════════════════════════
    public sealed class ApiExceptionHandler(
        IHostEnvironment environment,
        ILogger<ApiExceptionHandler> logger) : IExceptionHandler
    {
        /// <summary>回給前台的 errorCode。前台的常數在 src/constants/errorCodes.js，改名時兩邊要一起改</summary>
        public const string ErrorCode = "ServerError";

        /// <summary>只處理這個開頭的網址</summary>
        private const string ApiPathPrefix = "/api";

        /// <summary>是不是 API 的網址。Program.cs 的 UseWhen 也用這個判斷，兩邊才不會不一致</summary>
        public static bool IsApiRequest(HttpRequest request)
            => request.Path.StartsWithSegments(ApiPathPrefix);

        /// <returns>true＝已經處理好了；false＝不是 API，交給下一個處理方式（後台的錯誤頁）</returns>
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // Program.cs 只在 /api 裝了這個總機，這裡再檢查一次，避免以後被裝到別的地方時誤攔後台頁面
            if (!IsApiRequest(httpContext.Request))
            {
                return false;
            }

            // 錯誤代碼：ASP.NET Core 幫每一次請求編的流水號，裡面沒有個人資料，可以給客人看
            var traceId = httpContext.TraceIdentifier;

            // 只記方法和路徑，不記 ? 後面的部分和送來的資料：可能有個資、密碼、token
            logger.LogError(exception,
                "API 發生未處理的錯誤：{Method} {Path}（錯誤代碼 {TraceId}）",
                httpContext.Request.Method, httpContext.Request.Path, traceId);

            var result = new ApiResult<ServerErrorData>
            {
                Success = false,
                Message = $"系統發生錯誤，請稍後再試（錯誤代碼：{traceId}）",
                ErrorCode = ErrorCode,
                Data = new ServerErrorData(
                    traceId,
                    // 錯誤細節只在開發環境附上；由這裡判斷環境，不讓前端或設定檔決定
                    environment.IsDevelopment()
                        ? new ServerErrorDetail(exception.GetType().FullName ?? exception.GetType().Name, exception.Message)
                        : null)
            };

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
            return true;
        }
    }

    /// <summary>例外時 ApiResult 的 data</summary>
    /// <param name="TraceId">錯誤代碼，跟 message 裡的一樣，單獨放一份給程式取用</param>
    /// <param name="Detail">錯誤細節，只有開發環境才有；沒有的時候整個欄位不輸出</param>
    public sealed record ServerErrorData(
        string TraceId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ServerErrorDetail? Detail);

    /// <summary>錯誤細節（只有開發環境）。完整的錯誤紀錄（Stack Trace）在後端的 log，用錯誤代碼搜尋</summary>
    /// <param name="Type">錯誤的種類，例如 Microsoft.Data.SqlClient.SqlException</param>
    /// <param name="Message">錯誤的原因</param>
    public sealed record ServerErrorDetail(string Type, string Message);
}