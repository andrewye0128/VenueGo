using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 付款對帳（3-C-5）：主動去問綠界那些卡在「付款處理中」的付款。
    /// 綠界的通知可能永遠不會來（客人沒刷卡就離開、刷完馬上關掉視窗又沒開 ngrok），
    /// 不問的話，付款會一直卡在處理中：客人不能重新付款，時段也一直被佔著
    /// </summary>
    public interface IPaymentReconciliationService
    {
        /// <summary>
        /// 對帳一輪：找出該問的付款（送出夠久、最近沒問過），一筆一筆交給付款確認員。
        /// 一筆失敗不影響其他筆；被綠界擋（RateLimited）就停止這一輪
        /// </summary>
        /// <param name="cancellationToken">網站關閉時取消</param>
        /// <returns>這一輪每一筆的處理結果（寫 log 用）；沒有要問的就是空的</returns>
        Task<IReadOnlyList<EcpayPaymentDecision>> ReconcileAsync(CancellationToken cancellationToken = default);
    }
}