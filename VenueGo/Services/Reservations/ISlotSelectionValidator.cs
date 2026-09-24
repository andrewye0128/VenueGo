using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VenueGo.Models.ReservationModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 時段選取驗證服務：判斷一組時段能不能被預約。
    /// <para>
    /// 【為何與計價服務分開】兩者的呼叫時機不同：驗證失敗就不該計價。
    /// 而且期末的會員前台需要單獨呼叫驗證（前端送一組時段、後端回可不可以），
    /// 那個情境不需要順便算錢。
    /// </para>
    /// <para>
    /// 【為何一定要有伺服器端驗證】步驟 4 的畫面會用 JavaScript
    /// 讓使用者無法選出不連續的組合，但那只能防手誤。
    /// 任何人都可以用開發者工具改掉限制、直接送出任意時段，
    /// 所以後端必須重新查詢資料庫並完整驗證一次。
    /// 這與步驟 1 重新查證會員、步驟 2 重新查證場地是同一個原則。
    /// </para>
    /// </summary>
    public interface ISlotSelectionValidator
    {
        /// <summary>
        /// 驗證所選時段。檢查項目依序為：
        /// 是否為空、是否重複、是否超過單筆上限、每一格是否真的可預約、是否連續。
        /// </summary>
        /// <param name="venueId">場地 Id。</param>
        /// <param name="date">使用日期。</param>
        /// <param name="slotTimes">所選時段的起始時間，順序不限。</param>
        /// <returns>
        /// 驗證結果。通過時 <see cref="SlotSelectionResult.Slots"/> 會帶回
        /// 由伺服器端重新查詢的時段狀態（含單價），計價請使用這份資料。
        /// </returns>
        Task<SlotSelectionResult> ValidateAsync(
            int venueId,
            DateOnly date,
            IReadOnlyList<TimeOnly> slotTimes,
            CancellationToken cancellationToken = default);
    }
}
