using static System.Net.Mime.MediaTypeNames;

namespace VenueGo.Dtos.ContentDtos
{
    //用途 : 在最新消息詳細頁顯示 上一頁 or 下一頁的 news 簡要資訊
    public class NewsNavItemDto
    {
        //id
        public int Id { get; set; }
        //標題
        public string Title { get; set; } = string.Empty;
    }
}
