namespace VenueGo.ViewModels.MemberViewModels
{
    /// <summary>
    /// 會員詳細資料（Member/GetDetailJson 回傳給 Modal 用）。
    /// 屬性名稱刻意與原本回傳的匿名物件完全一致，前端 JS 不用改。
    /// </summary>
    public class MemberDetailViewModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        /// <summary>已格式化的生日（yyyy-MM-dd），未填寫時為「未填寫」。</summary>
        public string Birth { get; set; } = string.Empty;

        public int CumulativeConsumption { get; set; }
        public int CumulativeVisitTime { get; set; }
        public int NoShowCount { get; set; }
        public string Status { get; set; } = string.Empty;

        /// <summary>已格式化的註冊時間（yyyy-MM-dd HH:mm）。</summary>
        public string CreatedAt { get; set; } = string.Empty;
    }
}