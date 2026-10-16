namespace VenueGo.Dtos.ContentDtos
{
    //前台常見問題的一個分類 >> 供 GET /api/faqs 使用
    //分類代碼、中文名稱、順序都由後端決定,前台不用另外放分類常數
    public class FaqCategoryGroupDto
    {
        //分類代碼(前台頁籤、網址 ?category= 使用)
        public string Category { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;

        //這個分類底下顯示中的問題,已依後台排好的順序;沒有問題時是空清單
        public List<FaqItemDto> Faqs { get; set; } = new List<FaqItemDto>();
    }
}
