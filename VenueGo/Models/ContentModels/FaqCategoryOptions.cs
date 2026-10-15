namespace VenueGo.Models.ContentModels
{
    //常見問題的分類清單
    //提供 ContentController(驗證)、後台 View(下拉選單、頁籤、分組標題)、Factory(依分類分組)
    public static class FaqCategoryOptions
    {
        //Key = 分類代碼(存進 Faqs.Category),Value = 顯示的中文名稱
        //順序就是後台頁籤、列表分組與前台頁籤的順序
        public static readonly List<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("booking", "預約與付款"),
            new KeyValuePair<string, string>("venue", "場地與設施"),
            new KeyValuePair<string, string>("member", "會員帳號"),
            new KeyValuePair<string, string>("checkin", "報到入場"),
            new KeyValuePair<string, string>("review", "評論撰寫"),
        };

        //驗證:代碼是否在清單內(空值或不在清單內都回傳 false)
        public static bool IsValid(string? code)
        {
            return All.Any(x => x.Key == code);
        }

        //代碼 >> 中文名稱;查不到時回傳空字串
        public static string GetName(string? code)
        {
            return All.FirstOrDefault(x => x.Key == code).Value ?? string.Empty;
        }
    }
}
