namespace VenueGo.Dtos.VenueDtos
{
    //給 SportTypeIntroDto 使用 >> 收費標準(資料來源:SportTypePriceRules + SportTypePeakHours)
    //只有「有啟用中的價格規則」才會產生這個物件,否則 SportTypeIntroDto.Price 是 null
    public class SportTypePriceDto
    {
        //離峰價(元/小時);不分尖離峰的運動類型,這就是唯一的價格
        public int OffPeakPrice { get; set; }

        //尖峰價(元/小時);null = 所有營業日都不分尖離峰
        public int? PeakPrice { get; set; }

        //尖峰時段摘要,例如「每天 17:00」、「週二~週三 17:00、週四 15:00」
        //沒有尖峰時是空字串,不會是 null
        public string PeakSummary { get; set; } = string.Empty;
    }
}
