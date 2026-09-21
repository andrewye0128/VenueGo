using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.VenueViewModels
{
    //價格規則編輯表單 >> 專供 SportTypePriceRuleEdit 頁面使用
    public class SportTypePriceRuleEditViewModel
    {
        public int SportTypePriceRuleId { get; set; }   //隱藏欄位,識別要修改哪一筆

        //運動類型固定不能改(改的話會牽動唯一索引跟別筆資料衝突),
        //頁面上只顯示名稱當標籤,SportTypeId 用隱藏欄位帶回 Controller
        [ValidateNever]
        public string SportTypeName { get; set; } = string.Empty;
        public int SportTypeId { get; set; }

        [Display(Name = "尖峰起始時間")]
        public TimeOnly? PeakStartTime { get; set; }   //可為 null，代表不分尖峰/離峰

        [Display(Name = "尖峰價格")]
        [Range(0, int.MaxValue, ErrorMessage = "尖峰價格不可為負數")]
        public int PeakPrice { get; set; }

        [Display(Name = "離峰價格")]
        [Range(0, int.MaxValue, ErrorMessage = "離峰價格不可為負數")]
        public int OffPeakPrice { get; set; }

        [Display(Name = "啟用狀態")]
        public bool IsActive { get; set; }

        //尖峰起始時間下拉選單的選項(只會列出合法的整點時間)
        [ValidateNever]
        public IEnumerable<SelectListItem> PeakStartTimeOptions { get; set; }
    }
}
