namespace VenueGo.ViewModels.VenueViewModels
{
    //VenueIndex 頁面的整體 ViewModel >> 包住目前頁要顯示的分組資料,以及換頁需要的頁碼資訊
    //分頁單位是 SportType
    //TotalGroupCount / TotalPages 為分組數量,
    public class VenueIndexViewModel
    {
        //目前這一頁要顯示的分組,每個分組都是完整的,不會被攔腰截斷
        public List<VenueGroupViewModel> Groups { get; set; } = new List<VenueGroupViewModel>();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 3;

        //符合條件的分組總數(運動類型種類數),不是場地總數
        public int TotalGroupCount { get; set; }

        //總顯示頁數
        public int TotalPages
        {
            get
            {
                //沒有資料時,也要顯示「第1頁」,所以至少回傳1
                if (TotalGroupCount == 0)
                {
                    return 1;
                }

                //用double相除,才不會因為整數除法把小數點捨去
                double pageCountAsDouble = TotalGroupCount / (double)PageSize;

                //無條件進位:例如10筆分組、每頁3筆 → 3.33頁,要4頁才裝得下剩下的
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
