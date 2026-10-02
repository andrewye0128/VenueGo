using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    //各運動類型「每天」的尖峰起始時間 >> 一個運動類型固定 7 筆(星期日~星期六各一筆)
    //價格(尖峰價/離峰價)不在這裡,仍然在 SportTypePriceRules,這張表只管「每天幾點起算尖峰」
    public class CSportTypePeakHourWrap
    {
        //設定wrap class >> 三步驟
        private SportTypePeakHour _sportTypePeakHour;
        public SportTypePeakHour sportTypePeakHour { get { return _sportTypePeakHour; } set { _sportTypePeakHour = value; } }
        public CSportTypePeakHourWrap() { _sportTypePeakHour = new SportTypePeakHour(); }


        //綁定對應欄位
        [Key]
        public int SportTypePeakHourId { get { return _sportTypePeakHour.SportTypePeakHourId; } set { _sportTypePeakHour.SportTypePeakHourId = value; } }

        [Display(Name = "運動類型")]
        public int SportTypeId { get { return _sportTypePeakHour.SportTypeId; } set { _sportTypePeakHour.SportTypeId = value; } }

        //Entity存的是byte,對外一律用System.DayOfWeek型別,這裡做轉型(跟CWeekBusinessHourWrap同一個做法)
        [Display(Name = "星期")]
        public DayOfWeek DayOfWeek
        {
            get { return (DayOfWeek)_sportTypePeakHour.DayOfWeek; }
            set { _sportTypePeakHour.DayOfWeek = (byte)value; }
        }

        //null 代表這一天不分尖峰/離峰(整天都收離峰價),公休日也一律存 null
        [Display(Name = "尖峰起始時間")]
        public TimeOnly? PeakStartTime { get { return _sportTypePeakHour.PeakStartTime; } set { _sportTypePeakHour.PeakStartTime = value; } }

        public DateTime? UpdatedAt { get { return _sportTypePeakHour.UpdatedAt; } set { _sportTypePeakHour.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _sportTypePeakHour.UpdatedBy; } set { _sportTypePeakHour.UpdatedBy = value; } }
    }
}
