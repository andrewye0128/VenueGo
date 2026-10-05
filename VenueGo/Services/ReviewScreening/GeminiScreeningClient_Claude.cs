using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using VenueGo.Models.Options;

namespace VenueGo.Services.ReviewScreening
{
    /// <summary>評論預審的 AI 層：送一則（已遮蔽的）評論，拿回預審結果。</summary>
    public interface IReviewScreeningAi
    {
        /// <summary>有沒有設定金鑰與模型。沒有的話，背景工作直接跳過 AI 分析。</summary>
        bool IsEnabled { get; }

        /// <summary>目前用的模型名稱，存進 ReviewScreening.AiModel。</summary>
        string ModelName { get; }

        /// <param name="maskedContent">⚠️ 一定要是 ReviewTextGuard 遮蔽過的文字（PublicText），不能是原文。</param>
        Task<AiOutcome> AnalyzeAsync(byte starRating, string maskedContent, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 用 Gemini 的 OpenAI 相容介面做評論預審。
    ///
    /// ⚠️ 例外不往外丟：網路斷線、逾時、回應格式錯，全部轉成 AiOutcome。
    ///    呼叫它的是背景工作，一個例外沒接住，整個背景工作就停了，之後的評論都不會被分析。
    ///    但「失敗是無聲的」是期中踩過的坑（TimeService 那次），所以每一種失敗都要寫 log。
    /// </summary>
    public sealed class GeminiScreeningClient : IReviewScreeningAi
    {
        public const string HttpClientName = "GeminiScreening";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeminiScreeningClient> _logger;
        private readonly GeminiOptions _options;

        public GeminiScreeningClient(
            IHttpClientFactory httpClientFactory,
            ILogger<GeminiScreeningClient> logger,
            IOptions<GeminiOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        public bool IsEnabled => _options.IsConfigured;
        public string ModelName => _options.ScreeningModel;

        public async Task<AiOutcome> AnalyzeAsync(byte starRating, string maskedContent, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled) return AiOutcome.Fail("AI 預審尚未設定金鑰或模型");

            string url = _options.BaseUrl.TrimEnd('/') + "/chat/completions";
            string json = ReviewScreeningAi.BuildRequestBody(_options.ScreeningModel, starRating, maskedContent).ToJsonString();

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.SendAsync(request, cancellationToken);
                string body = await response.Content.ReadAsStringAsync(cancellationToken);

                var outcome = response.IsSuccessStatusCode
                    ? ReviewScreeningAi.ParseSuccessBody(body)
                    : ReviewScreeningAi.ParseErrorBody((int)response.StatusCode, body);

                LogOutcome(outcome);
                return outcome;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient 逾時會丟 TaskCanceledException；不是我們自己取消的，就是逾時
                _logger.LogWarning("AI 預審逾時");
                return AiOutcome.Retry("逾時");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AI 預審連線失敗");
                return AiOutcome.Retry("連線失敗：" + ex.Message);
            }
        }

        private void LogOutcome(AiOutcome outcome)
        {
            switch (outcome.Kind)
            {
                case AiOutcomeKind.Ok:
                    break;
                case AiOutcomeKind.Retryable:
                    _logger.LogWarning("AI 預審暫時失敗，稍後重試：{Error}", outcome.Error);
                    break;
                case AiOutcomeKind.Failed:
                    // 金鑰錯、模型名稱錯都會落在這裡，一定要讓人看得到
                    _logger.LogError("AI 預審失敗：{Error}", outcome.Error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Kind, "未定義的結果種類");
            }
        }
    }
}
