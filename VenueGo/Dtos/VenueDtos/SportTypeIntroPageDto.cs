namespace VenueGo.Dtos.VenueDtos
{
    //前台「場館資訊」頁的整包資料 >> GET /api/venues/intro 回傳
    //開放時間是全場館共用,只放一份在最外層,前台在每個運動類型區塊重複顯示
    //無障礙空間、頁首標題與場館照片寫死在前台,不在這裡
    public class SportTypeIntroPageDto
    {
        //開放時間,週一~週日固定 7 筆(資料表查不到的那天當成公休)
        public List<BusinessHourDayDto> BusinessHours { get; set; } = new List<BusinessHourDayDto>();

        //各運動類型的介紹,依運動類型 Id 排序
        //只包含存在的運動類型(IsActive = true),而且底下至少有一個存在的場地
        public List<SportTypeIntroDto> SportTypes { get; set; } = new List<SportTypeIntroDto>();
    }
}
