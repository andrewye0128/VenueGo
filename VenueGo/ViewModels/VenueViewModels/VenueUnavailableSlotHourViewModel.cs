namespace VenueGo.ViewModels.VenueViewModels
{
    //一個時段按鈕的資料 >> 專供 VenueUnavailableSlotManage 頁面使用
    public class VenueUnavailableSlotHourViewModel
    {
        public TimeOnly Time { get; set; }         //這個時段的起始時間,按鈕上顯示、送Toggle時要帶的值

        public bool IsUnavailable { get; set; }    //true=目前被標記不開放,false=開放中

        public string? Reason { get; set; }        //IsUnavailable=true時的不開放原因

        public bool IsPast { get; set; }           //這個時段是不是已經過去(只有Date==今天才可能true),true就不顯示操作按鈕

        //以下是預約資訊,這個時段沒有被預約時全部為null
        public int? ReservationId { get; set; }    //有值代表這個時段已被預約,也用來連到預約詳細頁

        public string? OrderNo { get; set; }       //訂單編號,查不到訂單時為null

        public string? MemberName { get; set; }

        public string? MemberEmail { get; set; }

        public string? ReservationStatusText { get; set; }   //預約狀態中文,例如「已確認」

        public string? ReservationTimeRange { get; set; }    //整筆預約的使用時間,例如「20:00–23:00」,關閉時段的確認視窗提醒會取消整筆

        public bool IsBooked => ReservationId.HasValue;
    }
}
