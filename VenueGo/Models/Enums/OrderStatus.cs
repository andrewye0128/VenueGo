using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum OrderStatus : byte
    {

        /// <summary>
        /// 訂單狀態（Orders.OrderStatus）。
        /// <para>
        /// 【帳務狀態】回答「這張單的錢走到哪」，只處理金流與帳務，
        /// 不重複記錄場地使用狀況。場地是否有效佔用請看 <see cref="ReservationStatus"/>。
        /// </para>
        /// <para>
        /// 【注意】本列舉的 0~2 與 <see cref="ReservationStatus"/> 語意相近，
        /// 但 3 之後即不相同（預約的 4 是已完成、訂單的 4 是已退款）。
        /// 程式中一律使用列舉名稱比對，絕對不要直接寫數字。
        /// </para>
        /// </summary>


        /// <summary>
        /// 待付款：訂單已建立，尚未收到款項。
        /// 命名刻意避開 Unpaid，以免與 <see cref="PaymentStatus.Unpaid"/> 混淆。
        /// </summary>
        [Display(Name = "待付款")]
        AwaitingPayment = 0,

        /// <summary>
        /// 已付款：款項已收到，訂單正式成立。
        /// </summary>
        [Display(Name = "已付款")]
        Paid = 1,

        /// <summary>
        /// 已取消：本張訂單不再需要收款。
        /// 取消人與原因記於 Reservations 的 CancelledBy / CancelReason，不另設列舉值。
        /// </summary>
        [Display(Name = "已取消")]
        Cancelled = 2,

        /// <summary>
        /// 已逾期：超過付款期限仍未收到款項，訂單自動失效。
        /// </summary>
        [Display(Name = "已逾期")]
        Expired = 3,

        /// <summary>
        /// 已退款：款項已退回會員。
        /// 退款流程細節記於 Refunds 表，見 <see cref="RefundStatus"/>。
        /// </summary>
        [Display(Name = "已退款")]
        Refunded = 4
    }
}
