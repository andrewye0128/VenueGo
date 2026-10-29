using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using VenueGo.Models.Options;
using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 主動查詢綠界訂單（QueryTradeInfo V5，官方文件 2890）。
    /// <para>
    /// 【只問、不改】這裡只負責「問」和「看懂回答」，不碰資料庫。問完怎麼處理由呼叫的地方決定，
    /// 所以收到失敗通知（3-C-4）、排程補查（3-C-5）都能共用
    /// </para>
    /// <para>
    /// 【先驗證再相信】回答的防偽章對了，而且確實是我們問的那一筆，才採用內容
    /// </para>
    /// <para>
    /// 【不要問太頻繁】問太快綠界會回 403，擋約 30 分鐘。只在需要確認的時候呼叫
    /// </para>
    /// </summary>
    public class EcpayTradeQueryService : IEcpayTradeQueryService
    {
        /// <summary>給 AddHttpClient 用的名字，ReservationModuleExtensions_F3 要用同一個</summary>
        public const string HttpClientName = "Ecpay";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TimeProvider _timeProvider;
        private readonly EcpayOptions _ecpay;
        private readonly ILogger<EcpayTradeQueryService> _logger;

        public EcpayTradeQueryService(
            IHttpClientFactory httpClientFactory,
            TimeProvider timeProvider,
            IOptionsSnapshot<EcpayOptions> ecpay,
            ILogger<EcpayTradeQueryService> logger)
        {
            _httpClientFactory = httpClientFactory;

            // 真實時間：TimeStamp 要給綠界檢查（3 分鐘內有效），不能跟著時光機
            _timeProvider = timeProvider;

            _ecpay = ecpay.Value;
            _logger = logger;
        }

        public async Task<EcpayTradeQueryResult> QueryAsync(
            string merchantTradeNo, CancellationToken cancellationToken = default)
        {
            if (!_ecpay.IsConfigured)
            {
                return EcpayTradeQueryResult.Unavailable(EcpayTradeQueryError.NotConfigured);
            }

            var (body, error) = await SendAsync(merchantTradeNo, cancellationToken);
            if (body is null)
            {
                return EcpayTradeQueryResult.Unavailable(error);
            }

            var result = Interpret(merchantTradeNo, body);

            _logger.LogInformation(
                "綠界查詢 {MerchantTradeNo}：{Status}（TradeStatus {TradeStatus}，{Error}）",
                merchantTradeNo, result.Status, result.Trade?.TradeStatus, result.Error);

            return result;
        }

        // ══ 送出查詢 ═══════════════════════════════

        /// <returns>Body：綠界回答的原文；沒收到回答時是 null，原因在 Error</returns>
        private async Task<(string? Body, EcpayTradeQueryError Error)> SendAsync(
            string merchantTradeNo, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var content = new FormUrlEncodedContent(BuildRequestFields(merchantTradeNo));
                using var response = await client.PostAsync(_ecpay.QueryUrl, content, ct);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.LogWarning(
                        "綠界查詢被擋（HTTP 403，查詢太頻繁，約 30 分鐘後才能再查）：{MerchantTradeNo}", merchantTradeNo);
                    return (null, EcpayTradeQueryError.RateLimited);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "綠界查詢失敗：HTTP {StatusCode}，{MerchantTradeNo}", (int)response.StatusCode, merchantTradeNo);
                    return (null, EcpayTradeQueryError.HttpError);
                }

                return (await response.Content.ReadAsStringAsync(ct), EcpayTradeQueryError.None);
            }
            // 逾時會丟 TaskCanceledException。但如果是呼叫的地方自己取消（例如網站正在關閉），就照常往外丟
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                _logger.LogWarning(ex, "綠界查詢連線失敗：{MerchantTradeNo}", merchantTradeNo);
                return (null, EcpayTradeQueryError.NetworkError);
            }
        }

        /// <summary>查詢要送的欄位（官方文件 2890 的必填欄位）</summary>
        private Dictionary<string, string> BuildRequestFields(string merchantTradeNo)
        {
            var fields = new Dictionary<string, string>
            {
                ["MerchantID"] = _ecpay.MerchantId,
                ["MerchantTradeNo"] = merchantTradeNo,
                ["TimeStamp"] = _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            };

            fields[EcpayCheckMac.FieldName] = EcpayCheckMac.Create(fields, _ecpay.HashKey, _ecpay.HashIV);
            return fields;
        }

        // ══ 看懂回答 ═══════════════════════════════

        private EcpayTradeQueryResult Interpret(string merchantTradeNo, string body)
        {
            var fields = ParseBody(body);
            if (!fields.ContainsKey(EcpayCheckMac.FieldName))
            {
                _logger.LogWarning("綠界查詢的回答格式不對：{MerchantTradeNo}", merchantTradeNo);
                return EcpayTradeQueryResult.Unavailable(EcpayTradeQueryError.InvalidResponse, body);
            }

            // 1. 防偽章：不對的話，內容一個字都不能相信
            if (!EcpayCheckMac.Verify(fields, _ecpay.HashKey, _ecpay.HashIV))
            {
                _logger.LogWarning("綠界查詢的回答防偽章不對：{MerchantTradeNo}", merchantTradeNo);
                return EcpayTradeQueryResult.Unavailable(EcpayTradeQueryError.InvalidMac, body, isMacValid: false);
            }

            // 2. 防偽章對了，再確認回答的是我們問的那一筆
            if (fields.GetValueOrDefault("MerchantID") != _ecpay.MerchantId
                || fields.GetValueOrDefault("MerchantTradeNo") != merchantTradeNo)
            {
                _logger.LogWarning("綠界查詢的回答跟問的不是同一筆：{MerchantTradeNo}", merchantTradeNo);
                return EcpayTradeQueryResult.Unavailable(EcpayTradeQueryError.Mismatch, body, isMacValid: true);
            }

            // 3. 交易狀態
            var tradeStatus = fields.GetValueOrDefault("TradeStatus") ?? string.Empty;
            EcpayTradeQueryStatus? status = tradeStatus switch
            {
                EcpayProtocol.TradeStatuses.Paid => EcpayTradeQueryStatus.Paid,
                EcpayProtocol.TradeStatuses.NotYetPaid => EcpayTradeQueryStatus.NotYetPaid,
                EcpayProtocol.TradeStatuses.Failed => EcpayTradeQueryStatus.Failed,
                EcpayProtocol.TradeStatuses.NotFound => EcpayTradeQueryStatus.NotFound,
                _ => null,
            };
            if (status is null)
            {
                _logger.LogWarning(
                    "綠界查詢回了不認得的 TradeStatus {TradeStatus}：{MerchantTradeNo}", tradeStatus, merchantTradeNo);
                return EcpayTradeQueryResult.Unavailable(EcpayTradeQueryError.UnknownTradeStatus, body, isMacValid: true);
            }

            var trade = new EcpayTradeInfo(
                MerchantTradeNo: merchantTradeNo,
                TradeNo: NullIfEmpty(fields.GetValueOrDefault("TradeNo")),
                TradeAmount: EcpayProtocol.ParseInt(fields.GetValueOrDefault("TradeAmt")),
                PaymentDate: EcpayProtocol.ParseDateTime(fields.GetValueOrDefault("PaymentDate")),
                PaymentType: NullIfEmpty(fields.GetValueOrDefault("PaymentType")),
                TradeStatus: tradeStatus);

            return EcpayTradeQueryResult.Answered(status.Value, trade, body);
        }

        /// <summary>
        /// 綠界的回答是 key=value&amp;key=value，而且值「沒有」做網址編碼（實測：中文、空白、/ 都是原樣）。
        /// 所以只切開、不解碼：解碼的話 + 會變成空白，防偽章就對不上。
        /// 值裡如果有 &amp; 會切錯，但這時防偽章一定對不上，會當成問不到（安全的失敗）
        /// </summary>
        private static Dictionary<string, string> ParseBody(string body)
        {
            var fields = new Dictionary<string, string>();
            foreach (var pair in body.Trim().Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var index = pair.IndexOf('=');
                if (index <= 0) continue;
                fields[pair[..index]] = pair[(index + 1)..];
            }
            return fields;
        }

        private static string? NullIfEmpty(string? value)
            => string.IsNullOrEmpty(value) ? null : value;
    }
}