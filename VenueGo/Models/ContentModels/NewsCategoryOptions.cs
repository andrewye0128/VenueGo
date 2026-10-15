namespace VenueGo.Models.ContentModels
{
    //最新消息的分類清單
    //提供ContentController(驗證)、後台 View(下拉選單、列表顯示)、Factory 與前台 API(中文名稱、預設圖)
    public static class NewsCategoryOptions
    {
        //用於存放最新消息類別的清單
        //Key = 分類代碼(存進 News.Category),Value = 顯示的中文名稱
        //順序就是後台下拉選單與前台頁籤的順序;「其他」固定放最後
        public static readonly List<KeyValuePair<string, string>> All = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("announcement", "場館公告"),
            new KeyValuePair<string, string>("venue", "場地異動"),
            new KeyValuePair<string, string>("event", "活動資訊"),
            new KeyValuePair<string, string>("promotion", "優惠資訊"),
            new KeyValuePair<string, string>("other", "其他"),
        };

        //驗證:代碼是否在清單內(空值或不在清單內都回傳 false)
        public static bool IsValid(string? code)
        {
            return All.Any(x => x.Key == code);
        }

        //依代碼尋找對應中文名稱 >> 查不到時回傳空字串
        public static string GetName(string? code)
        {
            //找出 Key 相同的那一項,找不到時 FirstOrDefault 回傳的 Value 是 null
            return All.FirstOrDefault(x => x.Key == code).Value ?? string.Empty;
        }

        //代碼 >> 分類預設圖的網址路徑(沒有上傳圖片時使用)
        //例:"event" >> "/images/news/defaults/event.png"
        public static string GetDefaultImagePath(string code)
        {
            return "/images/news/defaults/" + code + ".png";
        }
    }
}
