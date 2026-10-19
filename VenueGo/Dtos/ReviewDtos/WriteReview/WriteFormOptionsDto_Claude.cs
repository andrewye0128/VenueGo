namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    /// <summary>撰寫頁要顯示哪些欄位、初始值、字數上限。</summary>
    public sealed record WriteFormOptionsDto(
        bool ShowMentions,
        bool ShowAnonymous,
        bool CanChooseAnonymous,
        bool ShowPublic,
        WriteFormInitialDto Initial,
        int ContentMaxLength);
}
