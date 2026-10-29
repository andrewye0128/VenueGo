using VenueGo.Enums;
using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 記錄和綠界的往來（PaymentTransactions）。只新增、不修改：處理完、知道結果之後才記錄。
    /// 送出付款單的紀錄不在這裡：那一筆要跟「付款改成處理中」放在同一個交易裡（EcpayCheckoutService）
    /// </summary>
    public interface IPaymentTransactionRecorder
    {
        /// <summary>記錄一則綠界送來的付款結果（伺服器通知、瀏覽器帶回）</summary>
        /// <param name="paymentId">哪一筆付款</param>
        /// <param name="channel">從哪一條路送來</param>
        /// <param name="notification">整理過的欄位</param>
        /// <param name="isMacValid">防偽章有沒有通過</param>
        /// <param name="rawFields">綠界送來的全部欄位，原樣存進 RawData</param>
        /// <param name="processResult">處理的結果</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用</param>
        Task RecordNotificationAsync(
            int paymentId,
            EcpayNotificationChannel channel,
            EcpayPaymentNotification notification,
            bool isMacValid,
            IReadOnlyDictionary<string, string> rawFields,
            PaymentTransactionProcessResult processResult,
            CancellationToken cancellationToken = default);

        /// <summary>記錄一次主動查詢（QueryTradeInfo）</summary>
        /// <param name="paymentId">哪一筆付款</param>
        /// <param name="merchantTradeNo">問的是哪一張付款單</param>
        /// <param name="result">綠界的回答</param>
        /// <param name="processResult">依照回答處理的結果</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用</param>
        Task RecordQueryAsync(
            int paymentId,
            string merchantTradeNo,
            EcpayTradeQueryResult result,
            PaymentTransactionProcessResult processResult,
            CancellationToken cancellationToken = default);
    }
}