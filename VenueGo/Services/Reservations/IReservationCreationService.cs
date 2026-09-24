using System.Threading;
using System.Threading.Tasks;
using VenueGo.Models.ReservationModels;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 建立預約服務：把暫存的五個步驟寫進資料庫。
    /// <para>
    /// 【一次要寫五張表】Reservations、ReservationSlots、Orders、OrdersDetails、Payments，
    /// 再加一筆 AuditLogs。全部在同一個交易內完成，任一步失敗則全部撤銷。
    /// </para>
    /// <para>
    /// 【為何不寫在 Controller】這段流程有三道驗證、一個重試迴圈、
    /// 兩種不同的唯一鍵衝突要分別處理。寫在 Controller 會讓 Action 長到難以閱讀，
    /// 而且期末的會員前台 API 也要走同一套流程，屆時只能複製一份。
    /// </para>
    /// </summary>
    public interface IReservationCreationService
    {
        /// <summary>
        /// 依暫存資料與確認頁的輸入建立預約。
        /// <para>
        /// 所有資料都會在伺服器端重新查證：時段是否仍可預約、場地是否啟用、
        /// 人數是否超過容納上限、金額重新計算。
        /// 前端送來的只有使用人數、發票資訊與兩個勾選，不含任何金額。
        /// </para>
        /// </summary>
        /// <param name="draft">Session 中的暫存資料。</param>
        /// <param name="input">確認頁的表單輸入。</param>
        /// <param name="operatorUserId">操作人員的 Users.UserId，寫入 CreatedBy 與稽核紀錄。</param>
        Task<ReservationCreationResult> CreateAsync(
            ReservationDraft draft,
            ConfirmReservationInputModel input,
            int operatorUserId,
            CancellationToken cancellationToken = default);
    }
}