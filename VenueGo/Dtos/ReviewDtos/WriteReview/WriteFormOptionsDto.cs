namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    /// <summary>撰寫頁要顯示哪些欄位、初始值、字數上限。</summary>
    /// <param name="PublicBufferDays">
    /// 10/26：沒有館方回覆時，評論建立後幾天會出現在評論專區（ReviewPolicy.PublicBufferDays）。
    /// 撰寫頁的「公開此評論」底下提示顧客用；天數由後端給，前端不寫死。
    /// </param>
    public sealed record WriteFormOptionsDto(
        bool ShowMentions,
        bool ShowAnonymous,
        bool CanChooseAnonymous,
        bool ShowPublic,
        WriteFormInitialDto Initial,
        int ContentMaxLength,
        int PublicBufferDays);
}
