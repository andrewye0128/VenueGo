using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 記錄和綠界的往來。欄位怎麼對應到 PaymentTransactions 只寫在這裡，
    /// 收到通知（3-C-4）、排程補查（3-C-5）都用同一套
    /// </summary>
    public class PaymentTransactionRecorder : IPaymentTransactionRecorder
    {
        // RawData 裡的中文保持原樣，不要變成 \u5834，在 SSMS 查看比較好讀（同 EcpayCheckoutService）
        private static readonly JsonSerializerOptions RawDataJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private readonly dbVenueContext _db;
        private readonly ITimeService _timeService;

        public PaymentTransactionRecorder(dbVenueContext db, ITimeService timeService)
        {
            _db = db;
            _timeService = timeService;
        }

        public Task RecordNotificationAsync(
            int paymentId,
            EcpayNotificationChannel channel,
            EcpayPaymentNotification notification,
            bool isMacValid,
            IReadOnlyDictionary<string, string> rawFields,
            PaymentTransactionProcessResult processResult,
            CancellationToken cancellationToken = default)
        {
            return AddAsync(new PaymentTransaction
            {
                PaymentId = paymentId,
                MerchantTradeNo = notification.MerchantTradeNo,
                EventType = (byte)ToEventType(channel),
                TradeNo = notification.TradeNo,
                RtnCode = notification.RtnCode,
                RtnMsg = notification.RtnMsg,
                TradeAmount = notification.TradeAmount,
                EcpayPaymentDate = notification.PaymentDate,
                IsMacValid = isMacValid,
                ProcessResult = (byte)processResult,

                // 綠界送來的全部欄位。CheckMacValue 也存：它不是秘密，沒有金鑰也算不出來
                RawData = JsonSerializer.Serialize(rawFields, RawDataJsonOptions)
            }, cancellationToken);
        }

        public Task RecordQueryAsync(
            int paymentId,
            string merchantTradeNo,
            EcpayTradeQueryResult result,
            PaymentTransactionProcessResult processResult,
            CancellationToken cancellationToken = default)
        {
            return AddAsync(new PaymentTransaction
            {
                PaymentId = paymentId,
                MerchantTradeNo = merchantTradeNo,
                EventType = (byte)PaymentTransactionEventType.QueryTradeInfo,
                TradeNo = result.Trade?.TradeNo,

                // 查詢沒有 RtnCode，改存 TradeStatus（0 還沒付款、1 已付款、10200095 失敗）
                RtnCode = EcpayProtocol.ParseInt(result.Trade?.TradeStatus),

                // 問不到時記下原因，查問題用
                RtnMsg = result.Status == EcpayTradeQueryStatus.Unavailable ? $"問不到：{result.Error}" : null,

                TradeAmount = result.Trade?.TradeAmount,
                EcpayPaymentDate = result.Trade?.PaymentDate,
                IsMacValid = result.IsMacValid,
                ProcessResult = (byte)processResult,

                // 綠界回答的原文（沒收到回答是 null）
                RawData = result.RawBody
            }, cancellationToken);
        }

        private async Task AddAsync(PaymentTransaction record, CancellationToken ct)
        {
            record.CreatedAt = _timeService.Now;   // 網站時間；綠界的真實時間在 EcpayPaymentDate
            _db.PaymentTransactions.Add(record);
            await _db.SaveChangesAsync(ct);
        }

        private static PaymentTransactionEventType ToEventType(EcpayNotificationChannel channel) => channel switch
        {
            EcpayNotificationChannel.ServerNotify => PaymentTransactionEventType.ReturnUrl,
            EcpayNotificationChannel.BrowserReturn => PaymentTransactionEventType.OrderResultUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
        };
    }
}