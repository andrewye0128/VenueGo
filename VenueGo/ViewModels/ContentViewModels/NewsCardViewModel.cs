namespace VenueGo.ViewModels.ContentViewModels
{
    public class NewsCardViewModel
    {
        //最新消息的圖卡使用資訊
        public int NewsId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPinned { get; set; }

        //草稿 / 排程中 / 已發布,由 Factory 依現在時間算好
        public string StatusText { get; set; } = string.Empty;
    }
}
