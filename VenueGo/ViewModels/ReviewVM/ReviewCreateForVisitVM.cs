using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>現場評論撰寫頁（CreateForVisit）</summary>
    public class ReviewCreateForVisitVM : ReviewCreateInputVM
    {
        [Key]
        public int? ReviewPerVisitId { get; set; }

        /// <summary>憑證識別碼。POST 時與 ReviewPerVisitId 一起重查，兩者要對得上。</summary>
        [DisplayName("QR 條碼")]
        public string? QrToken { get; set; }

        // 顯示用，由 Controller 在 GET 時查好填入
        [DisplayName("場地名稱")]
        public string? VenueName { get; set; }

        [DisplayName("使用時段")]
        public DateTime? RentStartTime { get; set; }

        public override string? ContextPrimary => VenueName;

        public override string? ContextSecondary =>
            RentStartTime == null
                ? null
                : RentStartTime.Value.ToString("M/d HH:mm") + " 使用";
    }
}