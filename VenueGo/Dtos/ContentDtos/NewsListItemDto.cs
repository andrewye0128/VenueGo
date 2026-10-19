namespace VenueGo.Dtos.ContentDtos
{
    //提供 GetNewsList API 傳輸資料使用
    public class NewsListItemDto 
    {
        //id
        public int Id { get; set; }
        //標題
        public string Title { get; set; } = string.Empty;
        //是否釘選
        public bool IsPinned { get; set; }
        //分類標籤
        public string Category { get; set; } = string.Empty;
        //分類中文名稱
        public string CategoryName { get; set; } = string.Empty;
        //發布時間
        public DateTime PublishedAt { get; set; }
        //圖片路徑
        public string ImagePath { get; set; } = string.Empty;
        //內文摘要(100字以內)
        public string Summary { get; set; } = string.Empty;

    }
}
