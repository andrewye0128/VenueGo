using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    /// <summary>
    /// 退款狀態（Refunds.RefundStatus）。
    /// <para>
    /// 流程為「申請 → 審核 → 撥款 → 完成」，對應 Refunds 表的
    /// RequestedAt（申請時間）與 RefundedAt（完成時間）兩個欄位。
    /// </para>
    /// <para>
    /// 【整組皆為期末功能】期中不會用到。
    /// 若期末時間不足，可精簡為 Pending / Completed / Rejected / Failed 四個值，
    /// 但至少要保留「申請中」與「已完成」的區分，因為 RefundedAt
    /// 這個欄位就是給 <see cref="Completed"/> 使用的。
    /// </para>
    /// <para>
    /// 【資料結構備註】Refunds 掛在 PaymentId 之下且無唯一鍵，
    /// 結構上允許一筆付款多次退款（部分退款）。
    /// 期中規範為「一筆付款對一筆全額退款」，部分退款留待期末。
    /// </para>
    /// </summary>
    public enum RefundStatus : byte
    {
        /// <summary>
        /// 待審核：會員已申請退款，尚未經人工審核。
        /// </summary>
        [Display(Name = "待審核")]
        Pending = 0,

        /// <summary>
        /// 已核准：審核通過同意退款，但款項尚未送出。
        /// 扣款比例與金額記於 DeductionRate / DeductionAmount。
        /// </summary>
        [Display(Name = "已核准")]
        Approved = 1,

        /// <summary>
        /// 退款處理中：款項已送交金流，等待處理結果。
        /// </summary>
        [Display(Name = "退款處理中")]
        Processing = 2,

        /// <summary>
        /// 已完成：退款完成，此時才寫入 RefundedAt。
        /// </summary>
        [Display(Name = "已完成")]
        Completed = 3,

        /// <summary>
        /// 已駁回：不同意退款（超過可退期限、違反租借規則等）。
        /// 駁回原因記於 CancelReason。
        /// </summary>
        [Display(Name = "已駁回")]
        Rejected = 4,

        /// <summary>
        /// 退款失敗：已核准但執行失敗（帳號錯誤、金流異常等），需人工介入。
        /// </summary>
        [Display(Name = "退款失敗")]
        Failed = 5
    }
}
