using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CSportTypePriceRuleWrap
    {
        //設定wrap class >> 三步驟
        private SportTypePriceRule _sportTypePriceRule;
        public SportTypePriceRule sportTypePriceRule { get { return _sportTypePriceRule; } set { _sportTypePriceRule = value; } }
        public CSportTypePriceRuleWrap() { _sportTypePriceRule = new SportTypePriceRule(); }

        //綁定對應欄位
        [Key]
        public int SportTypePriceRuleId { get { return _sportTypePriceRule.SportTypePriceRuleId; } set { _sportTypePriceRule.SportTypePriceRuleId = value; } }

        [Display(Name = "運動類型")]
        public int SportTypeId { get { return _sportTypePriceRule.SportTypeId; } set { _sportTypePriceRule.SportTypeId = value; } }

        [Display(Name = "尖峰起始時間")]
        public TimeOnly? PeakStartTime { get { return _sportTypePriceRule.PeakStartTime; } set { _sportTypePriceRule.PeakStartTime = value; } }

        [Display(Name = "尖峰時段價格")]
        [Range(0, int.MaxValue, ErrorMessage = "價格不可為負數")]
        public int PeakPrice { get { return _sportTypePriceRule.PeakPrice; } set { _sportTypePriceRule.PeakPrice = value; } }

        [Display(Name = "離峰時段價格")]
        [Range(0, int.MaxValue, ErrorMessage = "價格不可為負數")]
        public int OffPeakPrice { get { return _sportTypePriceRule.OffPeakPrice; } set { _sportTypePriceRule.OffPeakPrice = value; } }

        public bool IsActive { get { return _sportTypePriceRule.IsActive; } set { _sportTypePriceRule.IsActive = value; } }

        public DateTime? UpdatedAt { get { return _sportTypePriceRule.UpdatedAt; } set { _sportTypePriceRule.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _sportTypePriceRule.UpdatedBy; } set { _sportTypePriceRule.UpdatedBy = value; } }
    }
}