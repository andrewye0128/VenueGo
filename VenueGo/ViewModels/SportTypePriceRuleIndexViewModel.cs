using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels
{
    //價格規則清單列表項目 >> 專供 SportTypePriceRuleIndex 頁面使用,純顯示用,不綁定 Entity,不參與表單驗證
    //SportName 是從 SportTypes 表 join 過來的欄位,不屬於 SportTypePriceRule 本身
    public class SportTypePriceRuleIndexViewModel
    {
        public int SportTypePriceRuleId { get; set; }
        public int SportTypeId { get; set; }
        [Display(Name = "運動種類")]
        public string SportName { get; set; } = string.Empty;
        [Display(Name = "尖峰開始時間")]
        public TimeOnly? PeakStartTime { get; set; }
        [Display(Name = "尖峰價格")]
        public int PeakPrice { get; set; }
        [Display(Name = "離峰價格")]
        public int OffPeakPrice { get; set; }
        public bool IsActive { get; set; }
    }
}
