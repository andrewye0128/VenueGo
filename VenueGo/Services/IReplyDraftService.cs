namespace VenueGo.Services
{
    /// <summary>給館方員工用的「AI 產生回覆草稿」。產出一定要經人工審核才送出。</summary>
    public interface IReplyDraftService
    {
        /// <summary>功能是否可用（有沒有設定金鑰）。給 View 決定要不要顯示按鈕。</summary>
        bool IsEnabled { get; }

        /// <summary>依評論內容產生一則回覆草稿。失敗時丟例外，由 Controller 轉成 JSON 錯誤。</summary>
        Task<string> DraftAsync(ReplyDraftInput input, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 要餵給 LLM 的資料。
    /// ⚠️ 刻意「只有這四個欄位」：不放真實姓名、電話、訂單編號、UserId。
    ///    給第三方的資料越少越好，這是個資保護的基本原則，
    ///    而且這四個欄位就足以寫出一則合格的回覆了。
    /// </summary>
    public record ReplyDraftInput(
        int StarRating,
        string? ReviewContent,
        string VenueName,
        string SportName);
}