using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum ReservationStatus : byte
    {
        /// <summary>
        /// 預約狀態（Reservations.ReservationStatus）。
        /// <para>
        /// 【主狀態】回答「這格場地還算不算被有效佔用」。
        /// 查詢預約狀態一律看這個欄位，不要改看 <see cref="OrderStatus"/>。
        /// </para>
        /// <para>
        /// 【佔位規則】只有 <see cref="Pending"/> 與 <see cref="Confirmed"/> 時，
        /// ReservationSlots 才會有對應資料列；其餘狀態代表佔位已釋放，
        /// 轉入這些狀態時必須同時刪除該筆預約的 ReservationSlots 列，
        /// 否則該時段會永久卡住無法再被預約。
        /// </para>
        /// </summary>



        /// <summary>
        /// 待確認：預約已建立但尚未收到款項，時段先行保留。
        /// <para>終止欄位：CancelledBy / CancelledAt / CancelReason 皆為 NULL。</para>
        /// <para>【期中會使用】新增預約時的預設值。</para>
        /// </summary>
        [Display(Name = "待確認")]
        Pending = 0,

        /// <summary>
        /// 已確認：款項已收到，場地確定歸該會員使用。
        /// <para>終止欄位：皆為 NULL。</para>
        /// <para>【期中會使用】管理員於訂單管理確認收款後切換。</para>
        /// </summary>
        [Display(Name = "已確認")]
        Confirmed = 1,

        /// <summary>
        /// 已取消：因會員因素退訂（臨時有事、不再需要場地等），屬正常退訂。
        /// <para>
        /// 終止欄位填寫：
        /// CancelledBy = 執行取消的管理員 UserId（會員來電代操作時亦填管理員）；
        /// CancelledAt = 當下時間；
        /// CancelReason = 會員提供的原因，例如「會員臨時加班無法前往」。
        /// </para>
        /// <para>
        /// 後續處理：刪除 ReservationSlots；Orders 轉
        /// <see cref="OrderStatus.Cancelled"/>；
        /// 已付款者依退款規則計算 Refunds.DeductionRate（可扣款）。
        /// </para>
        /// <para>【期中會使用】</para>
        /// </summary>
        [Display(Name = "已取消")]
        Cancelled = 2,

        /// <summary>
        /// 已逾期：超過付款期限（PaymentDueAt）仍未付款，由系統自動失效。
        /// <para>
        /// 終止欄位填寫：CancelledBy = NULL（非人工操作）；
        /// CancelledAt = 系統判定逾期的時間；
        /// CancelReason = 固定字串，例如「逾期未付款，系統自動取消」。
        /// </para>
        /// <para>
        /// 注意：管理員補登過去日期的預約，其 PaymentDueAt 會落在過去，
        /// 排程判定逾期時須排除 Source = <see cref="ReservationSource.Admin"/> 的資料，
        /// 或於建立時即標記為已付款。
        /// </para>
        /// <para>【期末功能】需搭配排程作業。</para>
        /// </summary>
        [Display(Name = "已逾期")]
        Expired = 3,

        /// <summary>
        /// 已完成：場地已實際使用完畢，本筆預約結案。
        /// <para>終止欄位：皆為 NULL（並非終止，而是正常結束）。</para>
        /// <para>【期末功能】需搭配報到管理或排程作業。</para>
        /// </summary>
        [Display(Name = "已完成")]
        Completed = 4,

        /// <summary>
        /// 場館取消：因場地維護、設備故障、天災或政策等館方因素，
        /// 由館方單方面取消預約，屬館方無法履約。
        /// <para>
        /// 終止欄位填寫：
        /// CancelledBy = 執行取消的管理員 UserId；
        /// CancelledAt = 當下時間；
        /// CancelReason = 館方因素說明，例如「A 館地板整修」。
        /// </para>
        /// <para>
        /// 後續處理：刪除 ReservationSlots；Orders 轉
        /// <see cref="OrderStatus.Cancelled"/>；
        /// 已付款者一律全額退款，Refunds.DeductionRate 必須為 0，不可扣款；
        /// 不計入會員退訂率與 Users.NoShowCount 等負向指標。
        /// </para>
        /// <para>
        /// 典型流程：管理員於場地管理建立 VenueUnavailableSlots 時，
        /// 須先偵測該時段是否已有 ReservationSlots 佔用，
        /// 列出受影響的預約並經管理員確認後，才在同一個交易中批次轉入此狀態。
        /// 切勿靜默取消他人預約。
        /// </para>
        /// <para>【期中會使用】</para>
        /// </summary>
        [Display(Name = "場館取消")]
        CancelledByVenue = 6,

        /// <summary>
        /// 已作廢：管理員建錯單或測試資料，視為從未發生。
        /// <para>
        /// 與 <see cref="Cancelled"/> 的差別在於「作廢不計入任何營運統計」，
        /// 報表分析子系統必須完全排除此狀態。
        /// </para>
        /// <para>
        /// 終止欄位填寫：
        /// CancelledBy = 執行作廢的管理員 UserId；
        /// CancelledAt = 當下時間；
        /// CancelReason = 作廢原因，例如「開單時選錯場地，已另開正確單」。
        /// </para>
        /// <para>
        /// 後續處理：刪除 ReservationSlots；Orders 轉
        /// <see cref="OrderStatus.Cancelled"/>；
        /// 若已誤收款項則全額退還，DeductionRate 為 0。
        /// </para>
        /// <para>【期中會使用】</para>
        /// </summary>
        [Display(Name = "已作廢")]
        Voided = 7


        //Pending = 0,      // 待確認
        //Confirmed = 1,    // 已確認
        //Cancelled = 2,    // 已取消
        //Expired = 3,      // 已逾期
        //Completed = 4     // 已完成
    }
}