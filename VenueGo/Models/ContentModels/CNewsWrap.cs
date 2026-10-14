using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using VenueGo.Models.Entities;

namespace VenueGo.Models.ContentModels
{
    public class CNewsWrap
    {
        //設定Wrap class >> 三步驟
        private News _news;

        //實體本身不是表單欄位,不參與驗證(驗證只看下面綁定的屬性)
        [ValidateNever]
        public News news { get { return _news; } set { _news = value; } }
        public CNewsWrap() { _news = new News(); }

        //綁定對應欄位
        [Key]
        public int NewsId { get { return _news.NewsId; } set { _news.NewsId = value; } }

        //只擋空值;代碼是否在清單內由 Controller 呼叫 NewsCategoryOptions.IsValid() 檢查
        [Required(ErrorMessage = "請選擇分類")]
        [Display(Name = "分類")]
        public string Category { get { return _news.Category; } set { _news.Category = value; } }

        [Required(ErrorMessage = "標題不可空白")]
        [StringLength(100, ErrorMessage = "標題最多 100 字")]
        [Display(Name = "標題")]
        public string Title { get { return _news.Title; } set { _news.Title = value; } }

        [Required(ErrorMessage = "內容不可空白")]
        [StringLength(4000, ErrorMessage = "內容最多 4000 字")]
        [Display(Name = "內容")]
        public string Content { get { return _news.Content; } set { _news.Content = value; } }

        //由後端依「有沒有上傳圖片」決定(上傳圖或分類預設圖),不是表單欄位,不參與驗證
        [ValidateNever]
        [Display(Name = "封面圖片")]
        public string ImagePath { get { return _news.ImagePath; } set { _news.ImagePath = value; } }

        //公版底稿的 4 個欄位(第二階段才使用)
        public string? CoverTitle { get { return _news.CoverTitle; } set { _news.CoverTitle = value; } }

        public string? CoverTitleStyles { get { return _news.CoverTitleStyles; } set { _news.CoverTitleStyles = value; } }

        public string? CoverText { get { return _news.CoverText; } set { _news.CoverText = value; } }

        public string? CoverTextStyles { get { return _news.CoverTextStyles; } set { _news.CoverTextStyles = value; } }

        [Required(ErrorMessage = "請選擇發布時間")]
        [Display(Name = "發布時間")]
        public DateTime PublishedAt { get { return _news.PublishedAt; } set { _news.PublishedAt = value; } }

        [Display(Name = "上架")]
        public bool IsPublished { get { return _news.IsPublished; } set { _news.IsPublished = value; } }

        [Display(Name = "置頂")]
        public bool IsPinned { get { return _news.IsPinned; } set { _news.IsPinned = value; } }

        public bool IsActive { get { return _news.IsActive; } set { _news.IsActive = value; } }

        public DateTime CreatedAt { get { return _news.CreatedAt; } set { _news.CreatedAt = value; } }

        public int CreatedBy { get { return _news.CreatedBy; } set { _news.CreatedBy = value; } }

        public DateTime? UpdatedAt { get { return _news.UpdatedAt; } set { _news.UpdatedAt = value; } }

        public int? UpdatedBy { get { return _news.UpdatedBy; } set { _news.UpdatedBy = value; } }
    }
}
