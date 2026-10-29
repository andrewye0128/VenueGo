using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>處理綠界送來的付款結果</summary>
    public interface IEcpayNotificationService
    {
        /// <summary>
        /// 處理一則付款結果（伺服器通知、瀏覽器帶回共用同一套規則）。
        /// 會先檢查防偽章；每一則都會記錄到 PaymentTransactions（找不到付款單的除外）
        /// </summary>
        /// <param name="fields">綠界送來的全部欄位。要全部傳進來，少一個欄位防偽章就算不對</param>
        /// <param name="channel">從哪一條路送來</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用</param>
        Task<EcpayNotificationResult> HandleAsync(
            IReadOnlyDictionary<string, string> fields,
            EcpayNotificationChannel channel,
            CancellationToken cancellationToken = default);
    }
}