namespace VenueGo.Dtos.ReviewDtos.PublicReviews
{
    /// <summary>某一頁第一則評論的星等與日期（10/23，評論專區長按快轉時顯示）。</summary>
    /// <remarks>只給日期、不給幾點：快轉時只需要知道「大概翻到哪裡」。JSON 的 date 是 "2026-10-19"。</remarks>
    public sealed record PageMarkDto(int Star, DateOnly Date);
}
