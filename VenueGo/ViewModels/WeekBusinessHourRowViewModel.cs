using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    //場館營業時間表單的其中一列(一天) >> 專供 WeekBusinessHourIndex 頁面使用
    public class WeekBusinessHourRowViewModel
    {
        public int BusinessHoursId { get; set; }   //隱藏欄位,識別這一列對應DB哪一筆

        public DayOfWeek DayOfWeek { get; set; }   //隱藏欄位,驗證失敗要重新顯示表單時用來重算DayName

        [ValidateNever]   //顯示用,由DayOfWeek算出來的中文名稱(例如"星期一"),不是使用者填的欄位,不參與驗證
        public string DayName { get; set; } = string.Empty;

        [Display(Name = "是否營業")]
        public bool IsOpen { get; set; }

        [Display(Name = "開始營業時間")]
        public TimeOnly? OpenTime { get; set; }

        [Display(Name = "結束營業時間")]
        public TimeOnly? CloseTime { get; set; }
    }
}
