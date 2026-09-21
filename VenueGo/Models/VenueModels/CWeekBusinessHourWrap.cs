using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CWeekBusinessHourWrap
    {
        //設定wrap class >> 三步驟
        private WeekBusinessHour _weekBusinessHour;
        public WeekBusinessHour weekBusinessHour { get { return _weekBusinessHour; } set { _weekBusinessHour = value; } }
        public CWeekBusinessHourWrap() { _weekBusinessHour = new WeekBusinessHour(); }


        //綁定對應欄位
        [Key]
        public int BusinessHoursId { get { return _weekBusinessHour.BusinessHoursId; } set { _weekBusinessHour.BusinessHoursId = value; } }

        //Entity存的是byte,對外一律用System.DayOfWeek型別,這裡做轉型,不另外寫Enum去對應
        [Display(Name = "星期")]
        public DayOfWeek DayOfWeek
        {
            get { return (DayOfWeek)_weekBusinessHour.DayOfWeek; }
            set { _weekBusinessHour.DayOfWeek = (byte)value; }
        }

        [Display(Name = "是否營業")]
        public bool IsOpen { get { return _weekBusinessHour.IsOpen; } set { _weekBusinessHour.IsOpen = value; } }

        [Display(Name = "開始營業時間")]
        public TimeOnly? OpenTime { get { return _weekBusinessHour.OpenTime; } set { _weekBusinessHour.OpenTime = value; } }

        [Display(Name = "結束營業時間")]
        public TimeOnly? CloseTime { get { return _weekBusinessHour.CloseTime; } set { _weekBusinessHour.CloseTime = value; } }

        public DateTime? UpdatedAt { get { return _weekBusinessHour.UpdatedAt; } set { _weekBusinessHour.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _weekBusinessHour.UpdatedBy; } set { _weekBusinessHour.UpdatedBy = value; } }
    }
}