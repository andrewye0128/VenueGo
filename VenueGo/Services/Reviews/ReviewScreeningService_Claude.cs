using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;

namespace VenueGo.Services.Reviews
{
    /// <summary>ReviewScreening／ReviewScreeningLabel 各欄位的代碼，跟建表腳本的 CHECK 約束一致。</summary>
    public static class ScreeningCodes
    {
        // ReviewScreening.AiStatus
        public const byte AiPending = 0;      // 待分析
        public const byte AiDone = 1;         // 完成
        public const byte AiFailed = 2;       // 失敗（NextAttemptAt 有值＝等待重試；NULL＝不再重試）
        public const byte AiNotNeeded = 3;    // 不需分析（只有星等、沒有文字）

        // ReviewScreeningLabel.LabelType
        public const byte LabelReason = 1;    // 處理建議（ReviewPolicy.SpamReasons 的索引）
        public const byte LabelTopic = 2;     // 主題（ReviewTopics 的代碼）

        // ReviewScreeningLabel.Source
        public const byte SourceRule = 1;
        public const byte SourceAi = 2;
        public const byte SourceStaff = 3;

        // ReviewScreening.VerifyResult
        public const byte VerifiedSpam = 1;   // 已標記為垃圾
        public const byte VerifiedNormal = 2; // 判定正常
    }

    /// <summary>評論預審：建立預審紀錄、背景分析。</summary>
    public interface IReviewScreeningService
    {
        /// <summary>
        /// 預審功能有沒有開。沒有設定 Gemini 金鑰的電腦一律關閉：
        /// 組員的資料庫沒有預審的兩張表，關閉時完全不碰那兩張表，才不會出錯。
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 評論存進資料庫之後（已經 SaveChanges、有 ReviewId）呼叫一次，建立預審紀錄。
        /// ⚠️ 不會丟例外：評論已經存好了，預審失敗不該讓顧客看到錯誤頁，只寫 log。
        /// </summary>
        Task CreateForReviewAsync(ReviewMain review, CancellationToken cancellationToken = default);

        /// <summary>背景工作每一輪呼叫：處理到期的預審，回傳處理了幾筆。</summary>
        Task<int> ProcessDueAsync(int maxCount, CancellationToken cancellationToken = default);
    }

    public sealed class ReviewScreeningService(
        dbVenueContext db,
        IReviewScreeningAi ai,
        ITimeService timeService,
        ILogger<ReviewScreeningService> logger) : IReviewScreeningService
    {
        private readonly dbVenueContext _db = db;
        private readonly IReviewScreeningAi _ai = ai;
        private readonly ITimeService _timeService = timeService;
        private readonly ILogger<ReviewScreeningService> _logger = logger;

        /// <summary>
        /// 每次失敗之後要等多久再試。三個間隔＝最多重試 3 次，加上第一次共 4 次。
        /// 從送出評論到放棄大約 20 分鐘；超過這個時間，員工多半已經自己看過這則評論了。
        /// </summary>
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
        };

        public bool IsEnabled => _ai.IsEnabled;

        // ════════════════════════════════════════════════════════
        //  建立預審紀錄（評論送出時）
        // ════════════════════════════════════════════════════════

        public async Task CreateForReviewAsync(ReviewMain review, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) return;

            ReviewScreening? screening = null;
            try
            {
                // 同一則評論只建一次（例如重送、或之後補跑舊評論時）
                if (await _db.ReviewScreenings.AnyAsync(s => s.ReviewId == review.ReviewId, cancellationToken))
                    return;

                bool ratingOnly = string.IsNullOrWhiteSpace(review.ReviewContent);

                screening = new ReviewScreening
                {
                    ReviewId = review.ReviewId,
                    // 規則層現在就能判斷；AI 分析完之後，AI 認為要優先處理的也會補上
                    IsPriority = !ratingOnly && ReviewTextGuard.Screen(review.ReviewContent).IsPriority,
                    AiStatus = ratingOnly ? ScreeningCodes.AiNotNeeded : ScreeningCodes.AiPending,
                    AiAttempts = 0,
                    CreatedAt = _timeService.Now
                };

                _db.ReviewScreenings.Add(screening);
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "建立評論預審紀錄失敗：ReviewId={ReviewId}", review.ReviewId);

                // 存失敗的那一筆要從追蹤中拿掉，不然同一個請求之後再 SaveChanges 時會再失敗一次
                if (screening != null)
                    _db.Entry(screening).State = EntityState.Detached;
            }
        }

        // ════════════════════════════════════════════════════════
        //  背景分析
        // ════════════════════════════════════════════════════════

        public async Task<int> ProcessDueAsync(int maxCount, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) return 0;

            DateTime now = _timeService.Now;

            // 待分析的，加上「失敗、而且到了重試時間」的；先來先處理
            var due = await _db.ReviewScreenings
                .Where(s => s.AiStatus == ScreeningCodes.AiPending
                         || (s.AiStatus == ScreeningCodes.AiFailed && s.NextAttemptAt != null && s.NextAttemptAt <= now))
                .OrderBy(s => s.CreatedAt)
                .Take(maxCount)
                .ToListAsync(cancellationToken);

            if (due.Count == 0) return 0;

            var ids = due.Select(s => s.ReviewId).ToList();
            var reviews = await _db.ReviewMains
                                   .Where(r => ids.Contains(r.ReviewId))
                                   .ToDictionaryAsync(r => r.ReviewId, cancellationToken);

            // 一則一則處理、一則一則存：其中一則出錯，前面已經完成的不會跟著不見
            foreach (var screening in due)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!reviews.TryGetValue(screening.ReviewId, out var review))
                {
                    _logger.LogWarning("預審紀錄找不到對應的評論，不再處理：ReviewId={ReviewId}", screening.ReviewId);
                    StopRetrying(screening);
                }
                else if (string.IsNullOrWhiteSpace(review.ReviewContent))
                {
                    screening.AiStatus = ScreeningCodes.AiNotNeeded;
                    screening.NextAttemptAt = null;
                }
                else
                {
                    await AnalyzeAsync(screening, review, cancellationToken);
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            return due.Count;
        }

        private async Task AnalyzeAsync(ReviewScreening screening, ReviewMain review, CancellationToken cancellationToken)
        {
            // ⚠️ 送給 AI 的一定是遮蔽後的文字：電話、Email 這些個資不能送出去
            string masked = ReviewTextGuard.MaskForPublic(review.ReviewContent);

            screening.AiAttempts++;
            var outcome = await _ai.AnalyzeAsync(review.StarRating, masked, cancellationToken);

            switch (outcome.Kind)
            {
                case AiOutcomeKind.Ok:
                    await ApplyResultAsync(screening, outcome.Result!, cancellationToken);
                    break;

                case AiOutcomeKind.Retryable:
                    // 第 n 次失敗後等 RetryDelays[n-1]；次數用完就放棄
                    if (screening.AiAttempts <= RetryDelays.Length)
                    {
                        screening.AiStatus = ScreeningCodes.AiFailed;
                        screening.NextAttemptAt = _timeService.Now + RetryDelays[screening.AiAttempts - 1];
                    }
                    else
                    {
                        _logger.LogWarning("評論預審重試 {Attempts} 次仍失敗，放棄：ReviewId={ReviewId}，最後的原因：{Error}",
                                           screening.AiAttempts, screening.ReviewId, outcome.Error);
                        StopRetrying(screening);
                    }
                    break;

                case AiOutcomeKind.Failed:
                    // 金鑰錯、模型名稱錯、答案不合格：重試也沒用（原因已經由 GeminiScreeningClient 寫進 log）
                    StopRetrying(screening);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "未定義的結果種類");
            }
        }

        private async Task ApplyResultAsync(ReviewScreening screening, AiScreeningResult result, CancellationToken cancellationToken)
        {
            screening.AiStatus = ScreeningCodes.AiDone;
            screening.NextAttemptAt = null;
            screening.AiAnalyzedAt = _timeService.Now;
            screening.AiModel = Truncate(_ai.ModelName, 60);
            screening.Summary = Truncate(result.Summary, ReviewScreeningAi.SummaryMaxLength);
            screening.NegativeIntensity = result.NegativeIntensity;
            screening.NeedsManager = result.NeedsManager;
            screening.ManagerReason = result.ManagerReason == null
                ? null
                : Truncate(result.ManagerReason, ReviewScreeningAi.ManagerReasonMaxLength);

            // AI 建議標記為垃圾、或需要主管處理，就要優先處理（規則層已經說要的，維持 true）
            if (result.SuggestedReasons.Count > 0 || result.NeedsManager)
                screening.IsPriority = true;

            // AI 的標籤：先清掉這則評論舊的 AI 標籤（理論上沒有，保險），再寫入這次的
            var oldLabels = await _db.ReviewScreeningLabels
                                     .Where(l => l.ReviewId == screening.ReviewId && l.Source == ScreeningCodes.SourceAi)
                                     .ToListAsync(cancellationToken);
            _db.ReviewScreeningLabels.RemoveRange(oldLabels);

            foreach (byte reason in result.SuggestedReasons.Distinct())
                _db.ReviewScreeningLabels.Add(NewLabel(screening.ReviewId, ScreeningCodes.LabelReason, reason));

            foreach (byte topic in result.TopicCodes.Distinct())
                _db.ReviewScreeningLabels.Add(NewLabel(screening.ReviewId, ScreeningCodes.LabelTopic, topic));
        }

        private static ReviewScreeningLabel NewLabel(int reviewId, byte type, byte code) => new()
        {
            ReviewId = reviewId,
            LabelType = type,
            LabelCode = code,
            Source = ScreeningCodes.SourceAi
        };

        /// <summary>失敗而且不再重試：AiStatus＝失敗、NextAttemptAt＝NULL（約束要求兩者一致）。</summary>
        private static void StopRetrying(ReviewScreening screening)
        {
            screening.AiStatus = ScreeningCodes.AiFailed;
            screening.NextAttemptAt = null;
        }

        private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max);
    }
}
