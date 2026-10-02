namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務的回傳資料 >> 一個場地的價格摘要(給場地卡片顯示用)
    //包在 UsableVenueInfo.Price 裡,一定有值;沒有價格時,裡面的價格是 null
    public class VenuePriceSummary
    {
        //離峰價(元/小時);null = 沒有價格規則或價格規則停用
        public int? OffPeakPrice { get; set; }

        //尖峰價(元/小時);null = 7 個營業日都沒有尖峰,或沒有價格規則、價格規則停用
        public int? PeakPrice { get; set; }

        //尖峰時段摘要,例如「每天 17:00」、「週二~週三 17:00、週四 15:00、週五~週日 17:00」
        //沒有尖峰或沒有價格時是空字串,不會是 null
        public string PeakSummary { get; set; } = string.Empty;
    }
}
