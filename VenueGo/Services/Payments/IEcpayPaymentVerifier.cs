using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 付款確認員：把「綠界怎麼說」變成「付款要改成什麼」。
    /// 收到通知（3-C-4）、排程對帳、處理中又按付款（3-C-5）共用，規則只有一份
    /// </summary>
    public interface IEcpayPaymentVerifier
    {
        /// <summary>
        /// 綠界說付款成功（通知或查詢的回答）：交給收款確認服務，再判斷是不是重複付款
        /// </summary>
        /// <param name="target">哪一筆付款</param>
        /// <param name="tradeNo">綠界的交易編號；沒有的話當成資料不完整，不收款</param>
        /// <param name="amount">綠界說收到多少錢；沒有的話當成資料不完整，不收款</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用</param>
        Task<EcpayPaymentDecision> ApplyPaidAsync(
            EcpayPaymentTarget target, string? tradeNo, int? amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// 問綠界這張付款單的結果，依回答處理，並把這次查詢記錄到 PaymentTransactions。
        /// 只處理「付款處理中、而且是最新那張」的付款單，其他的直接回 Ignored，不會去問綠界
        /// </summary>
        /// <param name="target">哪一筆付款</param>
        /// <param name="merchantTradeNo">要問的付款單（我們的交易編號）</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用</param>
        Task<EcpayPaymentDecision> VerifyAsync(
            EcpayPaymentTarget target, string merchantTradeNo, CancellationToken cancellationToken = default);
    }
}