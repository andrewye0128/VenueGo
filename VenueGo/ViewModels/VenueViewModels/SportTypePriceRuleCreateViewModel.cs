using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.VenueViewModels
{
    //價格規則新增表單 >> 專供 SportTypePriceRuleCreate 頁面使用
    public class SportTypePriceRuleCreateViewModel
    {
        [Display(Name = "運動類型")]
        [Required(ErrorMessage ="運動類型不可為空白")]
        public int SportTypeId { get; set; }   // int 不可能是 null，不加 [Required]

        [Display(Name = "尖峰起始時間")]
        public TimeOnly? PeakStartTime { get; set; }   // 可為 null，代表不分尖峰/離峰

        [Display(Name = "尖峰價格")]
        [Range(0, int.MaxValue, ErrorMessage = "尖峰價格不可為負數")]
        public int PeakPrice { get; set; }

        [Display(Name = "離峰價格")]
        [Range(0, int.MaxValue, ErrorMessage = "離峰價格不可為負數")]
        public int OffPeakPrice { get; set; }

        //下拉選單的資料
        [ValidateNever] //該欄位不參與驗證
        public IEnumerable<SelectListItem> SportTypes { get; set; }

        // 尖峰起始時間下拉選單的選項(只會列出合法的整點時間),由 Controller 從
        // CSportTypePriceRuleFactory.GetPeakStartTimeOptions() 帶入,View 不自己組選項
        // ⚠️ 目前來源是【暫時寫死】的營業時間常數,見 CSportTypePriceRuleFactory 上方的 TODO 說明
        [ValidateNever]
        public IEnumerable<SelectListItem> PeakStartTimeOptions { get; set; }
    }
}
