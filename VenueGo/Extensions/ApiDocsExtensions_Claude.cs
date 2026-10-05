using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  API 文件：OpenAPI（產生 JSON）＋ Scalar（顯示成網頁）（10/5 從 Program.cs 搬出來）
    //
    //  ── 為什麼獨立成一個檔案 ──────────────────────────────────────
    //  前台改用 JWT 之後，下面的「安全性方案」要從防偽 token 換成 Bearer token。
    //  集中在這裡，到時候只改這個檔案，Program.cs 不用動。
    //
    //  ── 網址（只在開發環境開啟）────────────────────────────────────
    //    JSON：/openapi/v1.json
    //    網頁：/scalar
    // ════════════════════════════════════════════════════════════════
    public static class ApiDocsExtensions
    {
        /// <summary>
        /// 只有這幾種方法會驗證防偽 token，跟 Front-Web 的 http.js（UNSAFE_METHODS）一致。
        /// GET 不驗，所以 Scalar 上也不該標示 GET 需要 token。
        /// </summary>
        private static readonly HttpMethod[] AntiforgeryMethods =
            [HttpMethod.Post, HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete];

        public static IServiceCollection AddApiDocs(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    // 宣告一種「把 token 放在標頭」的驗證方式，Scalar 會出現一個欄位讓人貼 token
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                    {
                        ["Antiforgery"] = new OpenApiSecurityScheme
                        {
                            Type = SecuritySchemeType.ApiKey,
                            In = ParameterLocation.Header,
                            Name = "RequestVerificationToken",
                            Description = "先打 GET /api/antiforgery/token，再貼上 XSRF-TOKEN Cookie 的值",
                        },
                    };

                    // 只套用在會驗證防偽 token 的方法上
                    foreach (var operation in document.Paths.Values
                                 .SelectMany(path => path.Operations)
                                 .Where(operation => AntiforgeryMethods.Contains(operation.Key)))
                    {
                        operation.Value.Security ??= [];
                        operation.Value.Security.Add(new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference("Antiforgery", document)] = [],
                        });
                    }
                    return Task.CompletedTask;
                });
            });

            return services;
        }

        /// <summary>提供 OpenAPI 的 JSON 與 Scalar 網頁。要不要開啟由 Program.cs 決定（目前只在開發環境）。</summary>
        public static WebApplication MapApiDocs(this WebApplication app)
        {
            app.MapOpenApi();               // JSON：/openapi/v1.json
            app.MapScalarApiReference();    // 網頁：/scalar

            return app;
        }
    }
}
