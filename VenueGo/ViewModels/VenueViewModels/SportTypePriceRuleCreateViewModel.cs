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

        [Display(Name = "尖峰價格")]
        [Required(ErrorMessage = "尖峰價格不可空白")]
        [Range(0, int.MaxValue, ErrorMessage = "尖峰價格不可為負數")]
        public int PeakPrice { get; set; }

        [Display(Name = "離峰價格")]
        [Required(ErrorMessage = "離峰價格不可空白")]
        [Range(0, int.MaxValue, ErrorMessage = "離峰價格不可為負數")]
        public int OffPeakPrice { get; set; }

        //下拉選單的資料
        [ValidateNever] //該欄位不參與驗證
        public IEnumerable<SelectListItem> SportTypes { get; set; }

        //每週尖峰時段 7 列(週一~週日),每一列各自有當天營業時間範圍內的下拉選項
        //新增時預設全部「不分尖峰/離峰」
        public List<SportTypePeakHourRowViewModel> Days { get; set; } = new List<SportTypePeakHourRowViewModel>();
    }
}
