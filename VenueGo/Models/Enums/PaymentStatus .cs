using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum PaymentStatus : byte
    {
        //Unpaid = 0,        // 未付款
        //Processing = 1,    // 付款處理中（已導向金流，等回調）
        //Paid = 2,          // 已付款
        //Failed = 3,        // 付款失敗
        //Cancelled = 4,     // 付款取消
        //Refunding = 5,     // 退款處理中
        //Refunded = 6       // 已退款


        /// <summary>
        /// 付款狀態（Payments.PaymentStatus）。
        /// <para>
        /// 【重要】預約列表的「付款狀態」篩選來源就是這個欄位，
        /// 因此建立預約的交易中必須一併新增一列 Payments
        /// （PaymentStatus = <see cref="Unpaid"/>、PaidAt = NULL），
        /// 否則列表 join 不到資料，付款狀態會顯示空白。
        /// </para>
        /// <para>
        /// 【與 RefundStatus 的分工】退款的流程細節請看 <see cref="RefundStatus"/>；
        /// 本列舉的 <see cref="Refunding"/> 與 <see cref="Refunded"/> 只是總結性標記，
        /// 且僅允許由退款流程統一更新，其他子系統不要直接修改這兩個值。
        /// </para>
        /// </summary>

        /// <summary>
        /// 未付款：尚未收到款項。
        /// 建立預約時的預設值。
        /// </summary>
        [Display(Name = "未付款")]
        Unpaid = 0,

        /// <summary>
        /// 付款處理中：已導向金流（如 LINE Pay）、等待回調結果。
        /// 這段空窗期不可視為未付款，否則系統會誤判無人付款而釋放時段，
        /// 導致會員付款完成後卻發現場地已被他人預約。
        /// 線上付款專用。
        /// </summary>
        [Display(Name = "付款處理中")]
        Processing = 1,

        /// <summary>
        /// 已付款：確實收到款項，同時應寫入 PaidAt。
        /// 現場收款後由管理員手動切換。
        /// </summary>
        [Display(Name = "已付款")]
        Paid = 2,

        /// <summary>
        /// 付款失敗：金流回報失敗（餘額不足、刷卡未過等）。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "付款失敗")]
        Failed = 3,

        /// <summary>
        /// 付款取消：會員在金流頁面中途放棄付款。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "付款取消")]
        Cancelled = 4,

        /// <summary>
        /// 退款處理中：退款流程進行中，細節見 Refunds 表。
        /// 【期末功能】
        /// </summary>
        [Display(Name = "退款處理中")]
        Refunding = 5,

        /// <summary>
        /// 已退款：款項已退回會員。
        /// 期中僅支援「一筆付款對一筆全額退款」，
        /// 部分退款留待期末處理。
        /// </summary>
        [Display(Name = "已退款")]
        Refunded = 6
    }
}