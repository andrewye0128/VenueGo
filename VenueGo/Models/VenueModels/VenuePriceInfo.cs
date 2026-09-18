namespace VenueGo.Models.VenueModels
{
    //對外顯示用途的價格資訊(例如場地卡片顯示尖峰/離峰價),不做時段判斷或加總
    //供其他子系統(例如預約模組)呼叫 CSportTypePriceRuleFactory.GetPriceRule() 取得
    public class VenuePriceInfo
    {
        //若該運動類型不分尖峰離峰,這裡固定回傳null,即使資料庫有存數值
        public int? PeakPrice { get; set; }

        public int OffPeakPrice { get; set; }
    }
}
