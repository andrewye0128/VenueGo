using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace VenueGo.Extensions
{
    // ════════════════════════════════════════════════════════════════
    //  API 文件：OpenAPI（產生 JSON）＋ Scalar（顯示成網頁）（10/5 從 Program.cs 搬出來）
    //
    //  ── 10/12 改成 JWT（Bearer）──────────────────────────────────
    //  前台 API 已經全面改用 JWT，Controllers/Api 底下也沒有 API 再驗防偽 token，
    //  所以原本的「防偽 token」安全性方案拿掉，換成 Bearer：
    //    - 只標在「用 JWT 驗證」的端點上（[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]）。
    //      Scalar 會在這些端點旁邊顯示鎖頭，送出時自動帶上你貼的 token。
    //    - 不需要登入的端點、後台 Cookie 驗證的端點（例如員工預覽）不標：
    //      後台端點在同一個網域、瀏覽器已經登入後台的話，Cookie 會自動帶上，不用另外貼。
    //
    //  ── 網址（只在開發環境開啟）────────────────────────────────────
    //    JSON：/openapi/v1.json
    //    網頁：/scalar
    // ════════════════════════════════════════════════════════════════
    public static class ApiDocsExtensions
    {
        private const string BearerSchemeName = "Bearer";

        public static IServiceCollection AddApiDocs(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                // 宣告一種「把 JWT 放在 Authorization 標頭」的驗證方式，Scalar 會出現一個欄位讓人貼 token
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "前台登入後拿到的 JWT。只貼 token 本身，不用加「Bearer 」",
                    };
                    return Task.CompletedTask;
                });

                // 只套用在「用 JWT 驗證」的端點上：看端點身上的 [Authorize] 有沒有指定 JwtBearer
                options.AddOperationTransformer((operation, context, cancellationToken) =>
                {
                    bool requiresJwt = context.Description.ActionDescriptor.EndpointMetadata
                        .OfType<IAuthorizeData>()
                        .Any(a => a.AuthenticationSchemes?.Split(',').Select(s => s.Trim())
                                   .Contains(JwtBearerDefaults.AuthenticationScheme) == true);
                    if (!requiresJwt) return Task.CompletedTask;

                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerSchemeName, context.Document)] = [],
                        });
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
