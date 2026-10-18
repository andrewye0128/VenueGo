using VenueGo.Helpers;

namespace VenueGo.Dtos
{
    /// <summary>
    /// 畫面上要顯示的時間：原始值＋「3 天前」＋完整時間（評論 API 規格 0-4）。
    /// Ago／Full 在後端用 TimeAgo 算好：規則只存在一個地方，而且「現在」是校時過的時間。
    /// 前台用 components/TimeText.vue 顯示。
    /// </summary>
    public sealed record TimeView(DateTime Value, string Ago, string Full)
    {
        public static TimeView From(DateTime time) => new(time, TimeAgo.Of(time), TimeAgo.Full(time));
    }
}
