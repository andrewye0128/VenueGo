namespace VenueGo.ViewModels.VenueViewModels
{
    //場地不開放時段管理整頁資料 >> 專供 VenueUnavailableSlotManage 頁面使用,一次顯示某場地某天的所有時段
    public class VenueUnavailableSlotManageViewModel
    {
        public int VenueId { get; set; }                       //Toggle表單要帶回去的場地id

        public string VenueName { get; set; } = string.Empty;  //頁首顯示場地名稱

        public string? PhotoPath { get; set; }                 //頁首顯示場地照片

        public DateOnly Date { get; set; }                     //目前查看的日期,日期切換連結靠它算前一天/後一天

        public bool IsBusinessDay { get; set; }                //這天有沒有營業,false就不畫任何時段按鈕

        public List<VenueUnavailableSlotHourViewModel> Hours { get; set; } = new List<VenueUnavailableSlotHourViewModel>();  //這天全部的時段按鈕
    }
}
