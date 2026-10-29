using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>主動問綠界某一張付款單的狀態（QueryTradeInfo）</summary>
    public interface IEcpayTradeQueryService
    {
        /// <summary>
        /// 問綠界這張付款單付了沒。只查詢，不改資料庫。
        /// 不會丟例外：連不上、逾時、被擋都回 Unavailable（呼叫的地方自己取消的話才會丟）
        /// </summary>
        /// <param name="merchantTradeNo">我們送出付款單時產生的交易編號（VG 開頭、20 碼）</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用（例如網站正在關閉）</param>
        Task<EcpayTradeQueryResult> QueryAsync(string merchantTradeNo, CancellationToken cancellationToken = default);
    }
}