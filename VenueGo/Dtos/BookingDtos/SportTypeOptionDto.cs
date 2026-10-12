namespace VenueGo.Dtos.BookingDtos
{
    /// <summary>
    /// 運動類型的選項（GET /api/sport-types）。
    /// 前台的運動類型下拉選單、預約頁的場地分組使用。
    /// <para>
    /// 欄位要跟前端 src/mocks/sportTypes.js 一致：sportTypeId、sportName、iconName。
    /// </para>
    /// </summary>
    public class SportTypeOptionDto
    {
        public int SportTypeId { get; init; }

        public string SportName { get; init; } = string.Empty;

        /// <summary>
        /// 圖示的短名稱，例如 basketball（資料庫目前全部是 NULL）。
        /// 前端 constants/sportTypes.js 會轉成完整的圖示名稱；null 時顯示通用圖示。
        /// </summary>
        public string? IconName { get; init; }
    }
}
