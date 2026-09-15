using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.ReviewVM
{
    /// <summary>預約評論撰寫頁（CreateForBooking）</summary>
    public class ReviewCreateForBookingVM : ReviewCreateInputVM
    {
        [Key]
        public int? ReviewPerBookingId { get; set; }

        /// <summary>對應 ReviewPerBooking.OrderId（原名 SourceId，已改名）。</summary>
        [DisplayName("訂單編號")]
        public int? OrderId { get; set; }

        [DisplayName("付款方式")]
        public byte? PaymentMethod { get; set; }

        /// <summary>訂單編號字串，取自 Orders.OrderNo，由 Controller 填入。</summary>
        public string? OrderNo { get; set; }

        public override string? ContextPrimary =>
            OrderNo ?? (OrderId == null ? null : $"訂單 #{OrderId}");

        // TODO: PaymentMethod 的代碼對照表要跟負責訂單的組員要，
        //       拿到之後把數字換成「信用卡」這類看得懂的字。
        public override string? ContextSecondary =>
            PaymentMethod == null ? null : $"付款方式代碼 {PaymentMethod}";
    }
}
