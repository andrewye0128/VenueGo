using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Helpers;
using VenueGo.Services.Reviews;

namespace VenueGo.Controllers
{
    // ════════════════════════════════════════════════════════════════
    //  評論預審的開發測試工具（昱）
    //
    //  丟一段文字進來，依序跑「規則層」與「AI 層」，把兩層的結果一起回傳。
    //  不讀也不寫資料庫，只用來確認：金鑰、模型名稱、網路、字詞清單都正常。
    //
    //  用法（瀏覽器直接打）：
    //      /DevScreening/Test?star=1&text=櫃台那個正妹電話給我0912345678
    //
    //  ⚠️ 只有開發環境能用，正式環境一律 404（不讓外人拿我們的額度去打 AI）。
    //  ⚠️ 會真的呼叫 Gemini，每打一次就用掉一次免費額度。
    // ════════════════════════════════════════════════════════════════
    [Route("DevScreening")]
    public sealed class DevScreeningController(IWebHostEnvironment env, IReviewScreeningAi ai) : Controller
    {
        private readonly IWebHostEnvironment _env = env;
        private readonly IReviewScreeningAi _ai = ai;

        [HttpGet("Test")]
        public async Task<IActionResult> Test(string? text, byte star = 3, CancellationToken cancellationToken = default)
        {
            if (!_env.IsDevelopment()) return NotFound();

            if (string.IsNullOrWhiteSpace(text) || star is < 1 or > 5)
                return Content("用法：/DevScreening/Test?star=1&text=評論內容（star 是 1～5）");

            // ── 規則層 ──
            var rule = ReviewTextGuard.Screen(text);

            // ── AI 層：送的是遮蔽後的文字，跟正式流程一樣 ──
            object ai;
            if (!_ai.IsEnabled)
            {
                ai = new { enabled = false, message = "沒有設定金鑰或模型：檢查 Secrets.json 的 Gemini:ApiKey 與 appsettings.json 的 Gemini:ScreeningModel" };
            }
            else
            {
                var watch = Stopwatch.StartNew();
                var outcome = await _ai.AnalyzeAsync(star, rule.PublicText, cancellationToken);
                watch.Stop();

                ai = new
                {
                    enabled = true,
                    model = _ai.ModelName,
                    elapsedMs = watch.ElapsedMilliseconds,
                    outcome = outcome.Kind.ToString(),   // Ok／Retryable（稍後重試）／Failed（重試也沒用）
                    error = outcome.Error,
                    result = outcome.Result is { } r
                        ? new
                        {
                            summary = r.Summary,
                            negativeIntensity = r.NegativeIntensity,
                            needsManager = r.NeedsManager,
                            managerReason = r.ManagerReason,
                            suggestedReasons = r.SuggestedReasons.Select(ReasonText).ToArray(),
                            topics = r.TopicCodes.Select(TopicText).ToArray()
                        }
                        : null
                };
            }

            return Json(new
            {
                input = new { star, text },
                rule = new
                {
                    publicText = rule.PublicText,
                    isPriority = rule.IsPriority,
                    hasPii = rule.HasPii,
                    suggestedReasons = rule.SuggestedReasons.Select(ReasonText).ToArray(),
                    hits = rule.Hits.Select(h => new
                    {
                        original = text.Substring(h.Start, h.Length),
                        kind = h.Kind.ToString(),
                        reason = h.Reason,
                        tier = h.Tier.ToString()
                    }).ToArray()
                },
                ai
            });
        }

        private static string ReasonText(byte index) =>
            index < ReviewPolicy.SpamReasons.Length ? $"{index} {ReviewPolicy.SpamReasons[index]}" : $"{index}（清單外）";

        private static string TopicText(byte code) =>
            ReviewTopics.All.FirstOrDefault(t => t.Code == code)?.Name ?? $"{code}（清單外）";
    }
}
