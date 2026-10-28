namespace VenueGo.Models.PaymentModels
{
    /// <summary>問綠界「這筆付了沒」的結論</summary>
    public enum EcpayTradeQueryStatus
    {
        /// <summary>已付款（TradeStatus 1）</summary>
        Paid,

        /// <summary>訂單成立但還沒付款（TradeStatus 0）：可能銀行還沒回覆，不是失敗</summary>
        NotYetPaid,

        /// <summary>交易失敗，客人沒有完成付款（TradeStatus 10200095）</summary>
        Failed,

        /// <summary>
        /// 綠界沒有這張付款單（TradeStatus 10200047）：付款單沒有送到綠界，所以不可能從這張單扣到錢
        /// </summary>
        NotFound,

        /// <summary>
        /// 問不到，結果不明（原因看 Error）。
        /// ⚠️ 呼叫的地方一律當成「還不知道」，絕對不能當成付款失敗，否則客人可能被扣兩次錢
        /// </summary>
        Unavailable,
    }

    /// <summary>問不到的原因：寫 log、決定 PaymentTransactions 要記什麼</summary>
    public enum EcpayTradeQueryError
    {
        None,

        /// <summary>網站沒有設定綠界</summary>
        NotConfigured,

        /// <summary>連不上、逾時</summary>
        NetworkError,

        /// <summary>HTTP 403：查詢太頻繁，綠界會擋約 30 分鐘</summary>
        RateLimited,

        /// <summary>其他不是 200 的回應</summary>
        HttpError,

        /// <summary>回答不是 key=value 的格式，或缺少防偽章</summary>
        InvalidResponse,

        /// <summary>回答的防偽章不對：可能被竄改，內容不能相信</summary>
        InvalidMac,

        /// <summary>回答的特店編號或交易編號，跟我們問的不一樣</summary>
        Mismatch,

        /// <summary>TradeStatus 是我們不認得的值（綠界之後新增的狀態）</summary>
        UnknownTradeStatus,
    }

    /// <summary>綠界回答的交易資料：只放會用到的欄位，完整內容在 EcpayTradeQueryResult.RawBody</summary>
    /// <param name="MerchantTradeNo">我們的交易編號（VG 開頭、20 碼）</param>
    /// <param name="TradeNo">綠界的交易編號（存進 Payments.TransactionNo）</param>
    /// <param name="TradeAmount">綠界說的金額，要跟訂單金額比對</param>
    /// <param name="PaymentDate">綠界的付款時間（真實時間）；還沒付款是 null</param>
    /// <param name="PaymentType">付款方式，例如 Credit_CreditCard；還沒付款是 null</param>
    /// <param name="TradeStatus">綠界原本的 TradeStatus，寫 log 用</param>
    public sealed record EcpayTradeInfo(
        string MerchantTradeNo,
        string? TradeNo,
        int? TradeAmount,
        DateTime? PaymentDate,
        string? PaymentType,
        string TradeStatus);

    /// <summary>查詢的結果。用 Answered、Unavailable 兩個方法建立，不直接 new</summary>
    /// <param name="Status">結論：已付款、還沒付款、失敗、問不到</param>
    /// <param name="Error">問不到的原因；有回答時是 None</param>    
    /// <param name="Trade">Status 不是 Unavailable 時才有值</param>
    /// <param name="RawBody">綠界回答的原文（存進 PaymentTransactions.RawData）；沒收到回答是 null</param>
    /// <param name="IsMacValid">防偽章有沒有通過（存進 PaymentTransactions.IsMacValid）；沒收到回答是 null</param>
    public sealed record EcpayTradeQueryResult(
        EcpayTradeQueryStatus Status,
        EcpayTradeQueryError Error,
        EcpayTradeInfo? Trade,
        string? RawBody,
        bool? IsMacValid)
    {
        /// <summary>綠界有回答，而且看得懂</summary>
        public static EcpayTradeQueryResult Answered(EcpayTradeQueryStatus status, EcpayTradeInfo trade, string rawBody)
            => new(status, EcpayTradeQueryError.None, trade, rawBody, IsMacValid: true);

        /// <summary>問不到，或回答不能相信</summary>
        public static EcpayTradeQueryResult Unavailable(
            EcpayTradeQueryError error, string? rawBody = null, bool? isMacValid = null)
            => new(EcpayTradeQueryStatus.Unavailable, error, Trade: null, rawBody, isMacValid);
    }
}