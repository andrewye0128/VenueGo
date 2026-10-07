namespace VenueGo.Dtos
{
    /// <summary>下拉選單或分頁的一個選項：Value 是送回後端的值，Text 是畫面上的文字。</summary>
    public sealed record OptionItem(string Value, string Text);
}
