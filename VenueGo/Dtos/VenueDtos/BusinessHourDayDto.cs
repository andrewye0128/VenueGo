namespace VenueGo.Dtos.VenueDtos
{
    //給 SportTypeIntroPageDto 使用 >> 開放時間的一天(資料來源:WeekBusinessHours)
    public class BusinessHourDayDto
    {
        //星期名稱,例如「週一」
        public string DayName { get; set; } = string.Empty;

        //是否營業;false = 公休,OpenTime、CloseTime 都是 null
        public bool IsOpen { get; set; }

        //開始營業時間,格式 "HH:mm"(例如 "10:00");公休時是 null
        //用字串而不是 TimeOnly:TimeOnly 轉成 JSON 會變成 "10:00:00",前台還要再處理
        public string? OpenTime { get; set; }

        //打烊時間,格式 "HH:mm";公休時是 null
        public string? CloseTime { get; set; }
    }
}
