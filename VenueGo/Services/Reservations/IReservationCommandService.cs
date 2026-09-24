using System.Threading;
using System.Threading.Tasks;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約狀態異動服務：負責改變預約的狀態並維持相關資料一致。
    /// <para>
    /// 【為何要集中在這裡】每一個異動都會同時改動三到四張表。
    /// 以取消為例，必須同時做到：改 Reservations 的狀態與取消欄位、
    /// 刪除 ReservationSlots 釋放時段、改 Orders 狀態、改 Payments 狀態、寫稽核紀錄。
    /// 少做任何一步都會產生看起來正常但實際壞掉的資料，
    /// 其中最嚴重的是漏刪 ReservationSlots——那個時段會永久卡住，
    /// 因為唯一鍵 UQ_ReservationSlots_Occupancy 不看預約狀態。
    /// </para>
    /// <para>
    /// 【誰會用到】預約詳細頁、訂單管理頁。兩者的操作相同，
    /// 共用這個服務才能保證兩邊的處理完全一致。
    /// </para>
    /// <para>
    /// 所有方法都在單一交易內完成，任一步失敗則全部撤銷。
    /// </para>
    /// </summary>
    public interface IReservationCommandService
    {
        /// <summary>
        /// 標記為已付款。
        /// <para>
        /// 同時更新 Payments（已付款 + 付款時間）、Orders（已付款）
        /// 與 Reservations（已確認）三個狀態。
        /// 只改付款狀態是不夠的：預約狀態若停在待確認，
        /// 列表上會出現「已付款但還沒確認」這種無法解釋的組合。
        /// </para>
        /// </summary>
        /// <param name="reservationId">預約 Id。</param>
        /// <param name="operatorUserId">操作人員的 Users.UserId，寫入稽核紀錄。</param>
        Task<ReservationCommandResult> MarkAsPaidAsync(
            int reservationId, int operatorUserId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取消預約（會員因素退訂）。計入會員退訂率，已收款者依規則可扣手續費。
        /// </summary>
        /// <param name="reason">取消原因，必填，寫入 Reservations.CancelReason。</param>
        Task<ReservationCommandResult> CancelAsync(
            int reservationId, string reason, int operatorUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 作廢預約（管理員開錯單或測試資料）。
        /// 與取消的差別在於作廢不計入任何營運統計，
        /// 且已誤收的款項一律全額退還，不可扣款。
        /// </summary>
        /// <param name="reason">作廢原因，必填。</param>
        Task<ReservationCommandResult> VoidAsync(
            int reservationId, string reason, int operatorUserId,
            CancellationToken cancellationToken = default);
    }
}