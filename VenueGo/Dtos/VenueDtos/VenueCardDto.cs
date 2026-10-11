namespace VenueGo.Dtos.VenueDtos
{
    public class VenueCardDto
    {
        //場地 Id,點擊"我要預約"時帶入 /booking?venueId=
        public int VenueId { get; set; }

        //場地名稱
        public string VenueName { get; set; } = string.Empty;

        //運動類型 Id,點擊"瀏覽介紹"時導向 /venues/{sportTypeId}、前台篩選分組使用
        public int SportTypeId { get; set; }

        //運動類型名稱,例如"羽球",圖卡標籤與篩選按鈕的文字
        public string SportTypeName { get; set; } = string.Empty;

        //場地照片的網址路徑(例如 /images/venues/xxx.jpg),沒有上傳照片時是 null
        public string? PhotoPath { get; set; }

    }
}
