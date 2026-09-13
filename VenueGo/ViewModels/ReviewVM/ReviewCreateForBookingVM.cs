using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace VenueGo.ViewModels.ReviewVM
{
    public class ReviewCreateForBookingVM : ReviewCreateInputVM
    {
        [Key]
        public int? ReviewPerBookingId { get; set; }

        /// <summary>
        /// 訂單編號。對應 ReviewPerBooking.OrderId
        /// （原名 SourceId，已由改名指令統一更名）。
        /// </summary>
        [DisplayName("訂單編號")]
        public int? OrderId { get; set; }

        [DisplayName("付款方式")]
        public byte? PaymentMethod { get; set; }

        public override string? ContextPrimary =>
            OrderId == null ? null : $"訂單 #{OrderId}";
        public override string? ContextSecondary =>
            PaymentMethod == null ? null : $"付款方式代碼 {PaymentMethod}";
        // TODO: 付款方式的代碼對照表要跟負責預約訂單的組員要，
        //       拿到之後把數字換成「信用卡」這類看得懂的字。
    }
}
