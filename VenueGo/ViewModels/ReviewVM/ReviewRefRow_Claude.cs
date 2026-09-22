namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>
    /// 館方清單一則評論的「周邊名稱」，對應檢視表 dbo.v_ReviewFullInfo 的一列。
    ///
    /// ── 這個類別為什麼不註冊進 dbVenueContext ─────────────────
    /// EF Core 8 之後，Database.SqlQuery&lt;T&gt; 支援「沒有對應到實體的型別」。
    /// 我們用的是 EF Core 10，所以這個類別不需要出現在 DbContext 裡，
    /// 也不需要有主鍵——剛好避開「不能改別人檔案」的限制。
    ///
    /// ⚠️ 唯一的規則：SELECT 出來的每一個欄位，這裡都必須有一個同名屬性。
    ///    所以查詢時不要寫 SELECT *，要把欄位列出來，兩邊對齊。
    ///    欄位對不起來的話是執行期才會炸，不是編譯期。
    /// </summary>
    public sealed class ReviewRefRow
    {
        public int ReviewId { get; set; }

        /// <summary>預約評論直接有；現場評論是 View 幫忙用 QRToken 轉過來的。追不到是 null。</summary>
        public int? OrderId { get; set; }
        public string? OrderNo { get; set; }

        /// <summary>只有現場評論有。預約評論是 null。</summary>
        public string? VenueName { get; set; }

        /// <summary>只有現場評論有。⚠️ 這個要顯示完整時間，不要用 TimeAgo。</summary>
        public DateTime? RentStartTime { get; set; }

        /// <summary>實名評論的會員姓名。匿名、或帳號已刪除時為 null。</summary>
        public string? UserName { get; set; }

        public string? ReadByEmployeeName { get; set; }
        public string? RepliedByEmployeeName { get; set; }
        public string? SpamMarkedByEmployeeName { get; set; }
    }
}
