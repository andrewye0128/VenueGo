namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務的回傳資料 >> 一個「可以使用」的場地
    //GetUsableVenueAsync、SearchUsableVenuesAsync 回傳
    //可以使用 = 場地存在(Venues.IsActive = true)而且它的運動類型也存在(SportTypes.IsActive = true)
    //不能使用的場地不會出現在這個類別裡(單筆查詢回傳 null、清單查詢不包含)
    public class UsableVenueInfo
    {
        public int VenueId { get; set; }

        //場地名稱,例如「羽球場-A」
        public string VenueName { get; set; } = string.Empty;

        //運動類型名稱,例如「羽球」
        public string SportName { get; set; } = string.Empty;

        //容納人數(驗證參加人數用);資料庫欄位允許空值,未設定時是 null
        public int? Capacity { get; set; }

        //照片的網址路徑(例如 /images/venues/xxx.jpg),可以直接放進 <img src>;沒有照片時是 null
        public string? PhotoPath { get; set; }

        //價格摘要,一定有值(沒有價格規則時,裡面的價格是 null)
        public VenuePriceSummary Price { get; set; } = new VenuePriceSummary();
    }
}
