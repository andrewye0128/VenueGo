using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.VenueViewModels
{
    //價格規則「每週尖峰時段」的其中一列(一天)
    //新增/編輯表單的 7 列表格、列表頁「明細」展開的 7 格,都共用這個 ViewModel
    //表單送回時,只有 DayOfWeek(隱藏欄位)跟 PeakStartTime(下拉選單)是使用者送回的值,
    //其他欄位都是顯示用,後端一律重新查營業時間算出來,不相信畫面送回的內容
    public class SportTypePeakHourRowViewModel
    {
        public DayOfWeek DayOfWeek { get; set; }   //隱藏欄位,識別這一列是星期幾

        [ValidateNever]   //顯示用,例如「週一」
        public string DayName { get; set; } = string.Empty;

        [ValidateNever]   //這天是否營業(來自 WeekBusinessHours),false 時整列灰色、選單停用
        public bool IsBusinessDay { get; set; }

        [ValidateNever]   //當天開始營業時間,顯示用
        public TimeOnly? OpenTime { get; set; }

        [ValidateNever]   //當天打烊時間,顯示用
        public TimeOnly? CloseTime { get; set; }

        [Display(Name = "尖峰起始時間")]
        public TimeOnly? PeakStartTime { get; set; }   //null 代表這天不分尖峰/離峰

        [ValidateNever]   //這天的下拉選單選項(不含「不分尖峰/離峰」,那個選項由 View 加在最前面)
        public List<SelectListItem> Options { get; set; } = new List<SelectListItem>();

        [ValidateNever]   //營業日的尖峰時間超出當天營業時間(營業時間之後被改過),畫面標成橘色提醒
        public bool IsOutOfRange { get; set; }
    }
}
