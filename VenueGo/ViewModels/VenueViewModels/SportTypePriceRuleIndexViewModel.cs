using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.VenueViewModels
{
    //價格規則清單列表項目 >> 專供 SportTypePriceRuleIndex 頁面使用,純顯示用,不綁定 Entity,不參與表單驗證
    //SportName 是從 SportTypes 表 join 過來的欄位,不屬於 SportTypePriceRule 本身
    public class SportTypePriceRuleIndexViewModel
    {
        public int SportTypePriceRuleId { get; set; }
        public int SportTypeId { get; set; }
        [Display(Name = "運動種類")]
        public string SportName { get; set; } = string.Empty;

        [Display(Name = "尖峰價格")]
        public int PeakPrice { get; set; }
        [Display(Name = "離峰價格")]
        public int OffPeakPrice { get; set; }
        public bool IsActive { get; set; }


        /***** 每天各自設定尖峰時間(SportTypePeakHours) *****/

        //尖峰時段摘要,例如「每天 17:00」、「週二~週五 17:00、週六~週日 15:00」
        //空字串代表所有營業日都不分尖峰/離峰,View 顯示「不分尖/離峰」
        [Display(Name = "尖峰時段")]
        public string PeakSummary { get; set; } = string.Empty;

        //是否有任何一個營業日有設定尖峰時間 >> 決定「尖峰價格」欄顯示金額還是「--」、要不要顯示「明細」按鈕
        public bool HasAnyPeak { get; set; }

        //是否有任何一個營業日的尖峰時間超出當天營業時間 >> 摘要旁顯示「超出營業時間」提醒
        public bool HasOutOfRange { get; set; }

        //還沒有任何一筆 SportTypePeakHours 資料(改版前就存在的舊價格規則) >> 顯示「尚未設定尖峰時段,請編輯」
        public bool IsPeakNotConfigured { get; set; }

        //7 天明細(週一~週日),給「明細」展開用
        public List<SportTypePeakHourRowViewModel> Days { get; set; } = new List<SportTypePeakHourRowViewModel>();
    }
}
