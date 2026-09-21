using System;
using System.Collections.Generic;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 建立預約的結果。
    /// <para>
    /// 【為何要區分時段衝突】一般的驗證失敗（人數超標、未勾選條款）只要顯示訊息、
    /// 讓使用者在同一頁改正即可；但時段被別人搶走無法在確認頁修正，
    /// 必須把使用者導回步驟 4 重新選擇，而且要重新載入時段表才看得到最新狀態。
    /// 兩種失敗的後續處理完全不同，因此用 <see cref="IsSlotConflict"/> 區分。
    /// </para>
    /// </summary>
    public class ReservationCreationResult
    {
        /// <summary>是否成功。</summary>
        public bool IsSuccess { get; private init; }

        /// <summary>成功時新建立的預約 Id，供導向詳細頁使用。</summary>
        public int ReservationId { get; private init; }

        /// <summary>成功時的訂單編號，供成功訊息顯示。</summary>
        public string? OrderNo { get; private init; }

        /// <summary>失敗時的訊息，已是可直接顯示給使用者的中文句子。</summary>
        public IReadOnlyList<string> Errors { get; private init; } = Array.Empty<string>();

        /// <summary>
        /// 失敗原因是否為時段衝突。
        /// 為 true 時呼叫端應把使用者導回步驟 4，而不是留在確認頁。
        /// </summary>
        public bool IsSlotConflict { get; private init; }

        /// <summary>建立成功的結果。</summary>
        public static ReservationCreationResult Success(int reservationId, string orderNo) => new()
        {
            IsSuccess = true,
            ReservationId = reservationId,
            OrderNo = orderNo
        };

        /// <summary>建立一般驗證失敗的結果，使用者可在確認頁改正。</summary>
        public static ReservationCreationResult Fail(params string[] errors) => new()
        {
            Errors = errors
        };

        /// <summary>建立一般驗證失敗的結果。</summary>
        public static ReservationCreationResult Fail(IReadOnlyList<string> errors) => new()
        {
            Errors = errors
        };

        /// <summary>
        /// 建立時段衝突的結果。呼叫端應導回步驟 4 並重新載入時段表。
        /// </summary>
        public static ReservationCreationResult SlotConflict(string message) => new()
        {
            Errors = new[] { message },
            IsSlotConflict = true
        };
    }
}