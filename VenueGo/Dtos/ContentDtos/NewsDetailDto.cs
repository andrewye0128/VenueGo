using static System.Net.Mime.MediaTypeNames;

namespace VenueGo.Dtos.ContentDtos
{
    //提供詳細最新消息內容使用
    public class NewsDetailDto
    {
        //id
        public int Id { get; set; }
        //標題
        public string Title { get; set; } = string.Empty;
        //內文
        public string Content { get; set; } = string.Empty;
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


        //上一頁 >> 用來顯示上一篇連結的簡要資訊
        public NewsNavItemDto? Previous { get; set; }
        //下一頁 
        public NewsNavItemDto? Next { get; set; }
    }
}
