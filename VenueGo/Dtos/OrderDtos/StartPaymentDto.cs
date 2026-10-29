namespace VenueGo.Dtos.OrderDtos
{
    /// <summary>
    /// 開始付款的回傳：前端用 ActionUrl＋Fields 建一個看不見的表單送出，畫面就跳到綠界。
    /// 欄位要跟前端 src/api/orderApi.js 的 startPayment 註解一致。
    /// <para>
    /// Fields 的 key 送到前端會維持原本的大小寫（MerchantID、CheckMacValue⋯）：
    /// ASP.NET Core 預設只把「屬性名稱」改成小寫開頭，Dictionary 的 key 不會改。
    /// 綠界的欄位名稱有分大小寫，改掉的話綠界會不認得
    /// </para>
    /// </summary>
    public class StartPaymentDto
    {
        /// <summary>綠界付款頁的網址（後端設定檔來的，前端還會再檢查一次是不是綠界的網址）</summary>
        public string ActionUrl { get; init; } = string.Empty;

        /// <summary>表單欄位，已經包含 CheckMacValue</summary>
        public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
    }
}