using System.ComponentModel.DataAnnotations;

namespace VenueGo.Dtos.ReviewDtos.WriteReview
{

    // ════════════════════════════════════════════════════════════════
    //  送出評論的請求本體（規格 2-3）
    //
    //  10/10 由 ViewModels/ReviewViewModels 的 ReviewCreateInputVM／ReviewCreateForVisitVM／ReviewCreateForBookingVM
    //  改名搬過來，並且只留下「前端會送來」的欄位：
    //    拿掉的：ReviewPerVisitId、QrToken、VenueName、RentStartTime（現場）、
    //            ReviewPerBookingId、OrderId、OrderNo、PaymentMethod（預約）、
    //            CanChooseAnonymous、ContextPrimary／ContextSecondary、[DisplayName]、[Key]。
    //    這些是 Razor 版用來「把資料帶到畫面上」的，API 版改由 WriteFormDto 回傳，
    //    留在請求本體裡只會讓 Scalar 文件看起來像「可以送這些欄位」。
    //    前端多送的欄位會被忽略（System.Text.Json 預設行為），所以前端不用改。
    //
    //  ⚠️ 欄位驗證的錯誤訊息就是前端看到的文字。
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 兩種評論共用的欄位。abstract：不會被單獨 new 出來，只當父類別。
    /// </summary>
    public abstract class ReviewCreateInputDto
    {
        [Required(ErrorMessage = "請選擇星等")]
        [Range(1, 5, ErrorMessage = "星等必須介於 1 到 5")]
        public byte? StarRating { get; set; }

        /// <summary>
        /// 字數上限只寫在這裡。撰寫頁表單回傳的 contentMaxLength 也是讀這個屬性，兩邊不會對不起來。
        /// </summary>
        [StringLength(1000, ErrorMessage = "評論內容不可超過 1000 字")]
        public string? ReviewContent { get; set; }

        /// <summary>提及場地。預約評論的表單目前不顯示這個選項，前端會送 false。</summary>
        public bool MentionsVenue { get; set; }

        /// <summary>提及服務。預約評論的表單目前不顯示這個選項，前端會送 false。</summary>
        public bool MentionsStaff { get; set; }
    }
}
