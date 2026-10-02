namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務的回傳資料 >> 一個「可以使用」的運動類型(SportTypes.IsActive = true)
    //GetUsableSportTypesAsync 回傳,給選場地頁的運動類型篩選選單使用
    public class UsableSportTypeInfo
    {
        //運動類型 Id,可以作為 SearchUsableVenuesAsync 的 sportTypeId 篩選條件
        public int SportTypeId { get; set; }

        //運動類型名稱
        public string SportName { get; set; } = string.Empty;
    }
}
