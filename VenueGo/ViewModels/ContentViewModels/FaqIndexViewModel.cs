using VenueGo.Models.ContentModels;

namespace VenueGo.ViewModels.ContentViewModels
{
    //常見問題後台列表頁 >> 目前的分類頁籤 + 依分類分組的問題(不分頁)
    public class FaqIndexViewModel
    {
        //目前的分類頁籤;null 代表「全部」
        public string? CurrentCategory { get; set; }

        //「全部」時有 5 組(順序同 FaqCategoryOptions.All),單一分類時只有 1 組
        public List<FaqGroupViewModel> Groups { get; set; } = new List<FaqGroupViewModel>();
    }

    //一個分類底下的問題,已依 SortOrder 排好
    public class FaqGroupViewModel
    {
        public string Category { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public List<CFaqWrap> Faqs { get; set; } = new List<CFaqWrap>();
    }
}
