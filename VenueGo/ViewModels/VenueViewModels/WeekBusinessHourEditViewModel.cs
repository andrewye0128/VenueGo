using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VenueGo.ViewModels.VenueViewModels
{
    //場館營業時間整頁表單 >> 專供 WeekBusinessHourIndex 頁面使用,一次顯示/送出7天
    public class WeekBusinessHourEditViewModel
    {
        public List<WeekBusinessHourRowViewModel> Days { get; set; } = new List<WeekBusinessHourRowViewModel>();

        //開始/結束營業時間下拉選單的選項(整點),7天共用同一份,不用每列各存一份
        [ValidateNever]
        public List<SelectListItem> TimeOptions { get; set; } = new List<SelectListItem>();
    }
}
