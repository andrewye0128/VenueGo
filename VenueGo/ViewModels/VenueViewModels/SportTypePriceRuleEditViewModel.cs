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

        [Display(Name = "尖峰價格")]
        [Required(ErrorMessage = "尖峰價格不可空白")]
        [Range(0, int.MaxValue, ErrorMessage = "尖峰價格不可為負數")]
        public int PeakPrice { get; set; }

        [Display(Name = "離峰價格")]
        [Required(ErrorMessage = "離峰價格不可空白")]
        [Range(0, int.MaxValue, ErrorMessage = "離峰價格不可為負數")]
        public int OffPeakPrice { get; set; }

        [Display(Name = "啟用狀態")]
        public bool IsActive { get; set; }

        //每週尖峰時段 7 列(週一~週日),帶入目前的設定
        //改版前的舊價格規則還沒有每日資料時,營業日會先用舊欄位的尖峰起始時間當預設值
        public List<SportTypePeakHourRowViewModel> Days { get; set; } = new List<SportTypePeakHourRowViewModel>();
    }
}
