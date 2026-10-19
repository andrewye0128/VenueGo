using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using VenueGo.Models.Entities;

namespace VenueGo.Models.ContentModels
{
    public class CFaqWrap
    {
        //設定Wrap class >> 三步驟
        private Faq _faq;

        //實體本身不是表單欄位,不參與驗證(驗證只看下面綁定的屬性)
        [ValidateNever]
        public Faq faq { get { return _faq; } set { _faq = value; } }
        public CFaqWrap() { _faq = new Faq(); }

        //綁定對應欄位
        [Key]
        public int FaqId { get { return _faq.FaqId; } set { _faq.FaqId = value; } }

        //只擋空值;代碼是否在清單內由 Controller 呼叫 FaqCategoryOptions.IsValid() 檢查
        [Required(ErrorMessage = "請選擇分類")]
        [Display(Name = "分類")]
        public string Category { get { return _faq.Category; } set { _faq.Category = value; } }

        [Required(ErrorMessage = "問題不可空白")]
        [StringLength(200, ErrorMessage = "問題最多 200 字")]
        [Display(Name = "問題")]
        public string Question { get { return _faq.Question; } set { _faq.Question = value; } }

        [Required(ErrorMessage = "回答不可空白")]
        [StringLength(1500, ErrorMessage = "回答最多 1500 字")]
        [Display(Name = "回答")]
        public string Answer { get { return _faq.Answer; } set { _faq.Answer = value; } }

        //同分類內的順序,由 Factory 決定,不是表單欄位
        public int SortOrder { get { return _faq.SortOrder; } set { _faq.SortOrder = value; } }

        [Display(Name = "顯示")]
        public bool IsPublished { get { return _faq.IsPublished; } set { _faq.IsPublished = value; } }

        public bool IsActive { get { return _faq.IsActive; } set { _faq.IsActive = value; } }

        public DateTime CreatedAt { get { return _faq.CreatedAt; } set { _faq.CreatedAt = value; } }

        public int CreatedBy { get { return _faq.CreatedBy; } set { _faq.CreatedBy = value; } }

        public DateTime? UpdatedAt { get { return _faq.UpdatedAt; } set { _faq.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _faq.UpdatedBy; } set { _faq.UpdatedBy = value; } }
    }
}
