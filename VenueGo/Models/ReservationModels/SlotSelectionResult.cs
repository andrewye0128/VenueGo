using System;
using System.Collections.Generic;

namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 時段選取的驗證結果。
    /// <para>
    /// 【為何不直接丟例外】驗證失敗是預期中的正常流程（使用者選錯了），
    /// 不是程式錯誤。用回傳值表達可以一次帶回多個錯誤訊息，
    /// 讓畫面一次告知使用者所有問題，而不是改一個、送出、再被擋一次。
    /// </para>
    /// </summary>
    public class SlotSelectionResult
    {
        /// <summary>是否通過驗證。</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>錯誤訊息。已是可直接顯示給使用者的中文句子。</summary>
        public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 驗證通過時，所選時段對應的完整狀態（含單價），依時間排序。
        /// <para>
        /// 這些資料由伺服器端重新查詢而來，不是前端送的。
        /// 計價一律使用這份資料，絕不接受前端傳入的金額。
        /// </para>
        /// </summary>
        public IReadOnlyList<TimeSlotStatus> Slots { get; init; }
            = Array.Empty<TimeSlotStatus>();

        /// <summary>建立驗證失敗的結果。</summary>
        public static SlotSelectionResult Fail(params string[] errors) => new()
        {
            Errors = errors
        };

        /// <summary>建立驗證失敗的結果。</summary>
        public static SlotSelectionResult Fail(IReadOnlyList<string> errors) => new()
        {
            Errors = errors
        };

        /// <summary>建立驗證成功的結果。</summary>
        public static SlotSelectionResult Success(IReadOnlyList<TimeSlotStatus> slots) => new()
        {
            Slots = slots
        };
    }
}