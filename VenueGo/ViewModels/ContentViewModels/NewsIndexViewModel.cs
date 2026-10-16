namespace VenueGo.ViewModels.ContentViewModels
{
    public class NewsIndexViewModel //最新消息後台列表頁面使用
    {
        //1. 目前這一頁的圖卡
        public List<NewsCardViewModel> Cards { get; set; } = new List<NewsCardViewModel>();

        //2. 目前的分類頁籤;null 代表「全部」
        public string? CurrentCategory { get; set; }

        //3. 分頁資訊
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }   //符合目前頁籤的消息總數

        //總顯示頁數
        public int TotalPages
        {
            get
            {
                //沒有資料時,也要顯示「第1頁」,所以至少回傳1
                if (TotalCount == 0)
                {
                    return 1;
                }

                //用double相除,才不會因為整數除法把小數點捨去
                double pageCountAsDouble = TotalCount / (double)PageSize;

                //無條件進位:例如25筆消息、每頁10筆 → 2.5頁,要3頁才裝得下剩下的
                int pageCount = (int)Math.Ceiling(pageCountAsDouble);

                return pageCount;
            }
        }

        //當前頁面是否有上一頁 & 下一頁
        public bool HasPreviousPage
        {
            get
            {
                //目前頁碼大於1,代表前面還有頁面可以回去
                if (CurrentPage > 1)
                {
                    return true;
                }

                return false;
            }
        }

        public bool HasNextPage
        {
            get
            {
                //目前頁碼小於總頁數,代表後面還有頁面可以前進
                if (CurrentPage < TotalPages)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
