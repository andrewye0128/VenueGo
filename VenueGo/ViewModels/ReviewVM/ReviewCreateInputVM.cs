using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.ReviewVM
{
    public class ReviewCreateInputVM // 評論撰寫頁
    {
        /// <summary>現場評論用；預約評論時為 null。</summary>
        [Key]
        public int? ReviewPerVisitId { get; set; }
        [DisplayName("QR 條碼")]
        public string? QrToken { get; set; }

        /// <summary>預約評論用；現場評論時為 null。</summary>
        [DisplayName("預約ID")]
        public int? ReservationId { get; set; }

        [DisplayName("評分")]
        [Required(ErrorMessage = "請選擇星等")]
        [Range(1, 5, ErrorMessage = "星等必須介於 1 到 5")]
        public byte? StarRating { get; set; }

        [DisplayName("評論內容")]
        [StringLength(1000, ErrorMessage = "評論內容不可超過 1000 字")]
        public string? ReviewContent { get; set; }

        [DisplayName("提及場地")]
        public bool MentionsVenue { get; set; }
        [DisplayName("提及服務")]
        public bool MentionsStaff { get; set; }

        [DisplayName("是否要匿名")]
        public bool IsAnonymous { get; set; }
        [DisplayName("是否公開")]
        public bool IsPublic { get; set; }

        // ── 給 View 顯示用的唯讀資訊（不參與繫結）──
        // 例如場地名稱、使用時段，讓顧客確認自己在評哪一筆
        [DisplayName("場地名稱")]
        public string? VenueName { get; set; }
        [DisplayName("使用時段")]
        public DateTime? RentStartTime { get; set; }

    }
}
