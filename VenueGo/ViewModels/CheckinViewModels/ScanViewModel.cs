namespace VenueGo.ViewModels.CheckinViewModels
{
    // 掃描驗票頁：只放 scan.js 需要的設定，畫面內容由 JavaScript 依掃描結果產生
    public class ScanViewModel
    {
        public string VerifyUrl { get; set; } = string.Empty;   // 驗票 API 網址（POST /Scan/Verify）
        public int VenueId { get; set; }                        // 這個掃描點所屬的場館
        public string VenueName { get; set; } = string.Empty;   // 標題顯示用
        public string DefaultMode { get; set; } = "in";         // 預設模式：in 入場 / out 離場
        public int PauseMilliseconds { get; set; } = 2500;      // 掃到一張後暫停多久，避免同一張重複送出
    }
}