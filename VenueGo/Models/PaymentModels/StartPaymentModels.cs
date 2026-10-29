namespace VenueGo.Models.PaymentModels
{
    /// <summary>開始付款的結果種類。Controller 用它決定 HTTP 狀態碼，名稱也當成 errorCode 給前端判斷</summary>
    public enum StartPaymentOutcome
    {
        /// <summary>成功：付款已改成處理中，回傳付款單</summary>
        Started,

        /// <summary>訂單不存在、不是自己的、沒有付款紀錄</summary>
        NotFound,

        /// <summary>現場付款的訂單</summary>
        NotOnlinePayment,

        /// <summary>已付款、退款處理中、已退款</summary>
        AlreadyPaid,

        /// <summary>上一筆還在付款處理中，結果不知道，不能再開新的付款單（3-C-5 會先問綠界）</summary>
        InProgress,

        /// <summary>預約不是待確認（已取消、已逾期⋯），或付款已取消</summary>
        NotPayable,

        /// <summary>超過最晚開始付款時間（付款期限前 N 分鐘）</summary>
        PastStartDeadline,

        /// <summary>金額不是正整數</summary>
        InvalidAmount,

        /// <summary>appsettings.Local.json 沒有綠界設定</summary>
        NotConfigured,

        /// <summary>檢查完到改狀態之間，付款狀態被別的操作改掉了（例如同時按了兩次）</summary>
        Conflict,

        /// <summary>交易編號連續撞號，放棄（幾乎不會發生）</summary>
        TradeNoUnavailable,
    }

    /// <summary>開始付款的結果</summary>
    /// <param name="Outcome">結果：成功，或不能付款的原因（前端用它當 errorCode）</param>
    /// <param name="Message">給客人看的中文說明</param>
    /// <param name="ActionUrl">綠界付款頁的網址（成功才有）</param>
    /// <param name="Fields">要送給綠界的欄位，已經包含 CheckMacValue（成功才有）</param>
    public sealed record StartPaymentResult(
        StartPaymentOutcome Outcome,
        string Message,
        string? ActionUrl = null,
        IReadOnlyDictionary<string, string>? Fields = null);
}