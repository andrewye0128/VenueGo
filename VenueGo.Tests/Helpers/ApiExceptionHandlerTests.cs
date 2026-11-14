using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using VenueGo.Helpers;

namespace VenueGo.Tests.Helpers
{
    /// <summary>
    /// ApiExceptionHandler（API 統一例外處理的「總機」）的單元測試。
    /// <para>
    /// 【為什麼要測】正式環境絕對不能把錯誤細節回給前台（資安），開發環境又要附上方便除錯。
    /// 這兩種行為只差一個 if，改壞了不容易發現，所以兩邊都測。
    /// </para>
    /// <para>
    /// 【怎麼測】不用啟動整個網站：自己做一個假的請求（DefaultHttpContext），
    /// 回應寫進記憶體（MemoryStream），再把寫出來的 JSON 讀回來檢查。
    /// 「現在是哪個環境」用 NSubstitute 做的假 IHostEnvironment 切換。
    /// </para>
    /// <para>
    /// 「Program.cs 有沒有裝在對的位置」單元測試測不到，要靠實際停掉 SQL Server 測試。
    /// </para>
    /// </summary>
    public class ApiExceptionHandlerTests
    {
        private const string TraceId = "0HTEST000001:00000002";

        // 故意放一串「像密碼」的字：正式環境的回應裡絕對不能出現
        private const string SecretInException = "P@ssw0rd-SECRET";

        private readonly RecordingLogger _logger = new();

        // ════════════════════════════════════════════════════════
        //  共用的小工具
        // ════════════════════════════════════════════════════════

        private ApiExceptionHandler CreateHandler(string environmentName)
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);
            return new ApiExceptionHandler(environment, _logger);
        }

        private static DefaultHttpContext CreateContext(string path = "/api/reservations")
        {
            var context = new DefaultHttpContext { TraceIdentifier = TraceId };
            context.Request.Method = "GET";
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static Exception CreateException()
            => new InvalidOperationException($"資料庫連線失敗，連線字串 Password={SecretInException}");

        /// <summary>把寫進記憶體的回應讀回來</summary>
        private static string ReadBody(HttpContext context)
        {
            context.Response.Body.Position = 0;
            return new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEnd();
        }

        /// <summary>處理一次錯誤，回傳 (有沒有處理, 回應的 JSON)</summary>
        private static async Task<(bool Handled, JsonElement Body, string Raw)> HandleAsync(
            ApiExceptionHandler handler, HttpContext context)
        {
            bool handled = await handler.TryHandleAsync(context, CreateException(), CancellationToken.None);
            string raw = ReadBody(context);
            JsonElement body = raw.Length == 0 ? default : JsonDocument.Parse(raw).RootElement;
            return (handled, body, raw);
        }

        // ════════════════════════════════════════════════════════
        //  回應的格式
        // ════════════════════════════════════════════════════════

        [Fact]
        public async Task TryHandleAsync_API發生錯誤_回500和ServerError()
        {
            var context = CreateContext();

            var (handled, body, _) = await HandleAsync(CreateHandler(Environments.Production), context);

            handled.ShouldBeTrue();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
            body.GetProperty("success").GetBoolean().ShouldBeFalse();
            body.GetProperty("errorCode").GetString().ShouldBe(ApiExceptionHandler.ErrorCode);
        }

        [Fact]
        public async Task TryHandleAsync_訊息和Data_都有同一個錯誤代碼()
        {
            var (_, body, _) = await HandleAsync(CreateHandler(Environments.Production), CreateContext());

            body.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain(TraceId);
            body.GetProperty("data").GetProperty("traceId").GetString().ShouldBe(TraceId);
        }

        // ════════════════════════════════════════════════════════
        //  錯誤細節：正式環境不附、開發環境附
        // ════════════════════════════════════════════════════════

        [Fact]
        public async Task TryHandleAsync_正式環境_回應裡沒有錯誤細節()
        {
            var (_, body, raw) = await HandleAsync(CreateHandler(Environments.Production), CreateContext());

            // 整個回應裡都找不到錯誤原因（包括那串像密碼的字），也沒有 detail 欄位
            raw.ShouldNotContain(SecretInException);
            raw.ShouldNotContain(nameof(InvalidOperationException));
            body.GetProperty("data").TryGetProperty("detail", out _).ShouldBeFalse();
        }

        [Fact]
        public async Task TryHandleAsync_開發環境_附上錯誤的種類和原因()
        {
            var (_, body, _) = await HandleAsync(CreateHandler(Environments.Development), CreateContext());

            var detail = body.GetProperty("data").GetProperty("detail");
            detail.GetProperty("type").GetString().ShouldBe(typeof(InvalidOperationException).FullName);
            detail.GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("資料庫連線失敗");
        }

        // ════════════════════════════════════════════════════════
        //  log 和看守範圍
        // ════════════════════════════════════════════════════════

        [Fact]
        public async Task TryHandleAsync_API發生錯誤_記成Error並附上錯誤代碼()
        {
            await HandleAsync(CreateHandler(Environments.Production), CreateContext());

            var entry = _logger.Entries.ShouldHaveSingleItem();
            entry.Level.ShouldBe(LogLevel.Error);
            entry.Message.ShouldContain(TraceId);
            entry.Exception.ShouldBeOfType<InvalidOperationException>();   // 完整的錯誤紀錄要記進 log
        }

        [Theory]
        [InlineData("/Reservations/Index")]   // 後台頁面
        [InlineData("/apidocs")]              // 開頭是 api 但不是 /api/ 底下：不算
        public async Task TryHandleAsync_不是API的網址_不處理(string path)
        {
            var context = CreateContext(path);

            var (handled, _, raw) = await HandleAsync(CreateHandler(Environments.Production), context);

            // 回 false，交給後台原本的錯誤頁；回應完全沒動、也沒記 log
            handled.ShouldBeFalse();
            raw.ShouldBeEmpty();
            context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
            _logger.Entries.ShouldBeEmpty();
        }

        // ════════════════════════════════════════════════════════
        //  記錄 log 的假 logger：把每一筆記下來，讓測試檢查
        //  （NSubstitute 不好檢查 LogError 這種擴充方法，自己寫一個比較清楚）
        // ════════════════════════════════════════════════════════

        private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

        private sealed class RecordingLogger : ILogger<ApiExceptionHandler>
        {
            public List<LogEntry> Entries { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}