namespace VenueGo.ViewModels.VenueViewModels
{
    //一個時段按鈕的資料 >> 專供 VenueUnavailableSlotManage 頁面使用
    public class VenueUnavailableSlotHourViewModel
    {
        public TimeOnly Time { get; set; }         //這個時段的起始時間,按鈕上顯示、送Toggle時要帶的值

        public bool IsUnavailable { get; set; }    //true=目前被標記不開放,false=開放中

        public string? Reason { get; set; }        //IsUnavailable=true時的不開放原因

        public bool IsPast { get; set; }           //這個時段是不是已經過去(只有Date==今天才可能true),true就disable按鈕
    }
}
