namespace VenueGo.Services.VenueSchedules
{
    //場地時段服務的回傳資料 >> 某場地某一天的「一格時段」(每格一小時)
    //GetDayScheduleAsync、GetRangeScheduleAsync 回傳的清單,每一個元素就是一格
    //只描述場地模組負責的資訊(營業、不開放、尖峰、單價);
    //「已被預約」「已過時間」屬於預約模組的判斷,不在這裡
    public class VenueSlotInfo
    {
        //這一格的開始時間,整點(例如 17:00 代表 17:00~18:00)
        public TimeOnly SlotTime { get; set; }

        //true = 管理員把這一格設為不開放(預約模組顯示為「維護中」)
        public bool IsUnavailable { get; set; }

        //true = 這一格收尖峰價:當天有尖峰起始時間,而且這一格的開始時間 >= 它
        //沒有價格規則或價格規則停用時一律是 false
        public bool IsPeak { get; set; }

        //這一格的單價(元):尖峰就是尖峰價,否則是離峰價
        //null = 沒有價格規則或價格規則停用,能不能預約由預約模組決定
        public int? UnitPrice { get; set; }
    }
}
