namespace VenueGo.Models.ReservationModels
{
    /// <summary>
    /// 預約狀態異動的結果。
    /// <para>
    /// 【為何用回傳值而非例外】「這筆預約已經取消過了」「查無此預約」
    /// 都屬於預期中的正常結果，不是程式錯誤。
    /// 用回傳值表達，呼叫端就能直接把訊息顯示給使用者，
    /// 而不必為了取出錯誤訊息去攔截例外。
    /// 真正的例外（資料庫斷線、唯一鍵衝突）仍然往外拋。
    /// </para>
    /// </summary>
    public class ReservationCommandResult
    {
        /// <summary>是否成功。</summary>
        public bool IsSuccess { get; private init; }

        /// <summary>失敗時的訊息，已是可直接顯示給使用者的中文句子。成功時為 null。</summary>
        public string? ErrorMessage { get; private init; }

        /// <summary>成功時的訊息，供畫面顯示成功提示。失敗時為 null。</summary>
        public string? SuccessMessage { get; private init; }

        /// <summary>建立成功的結果。</summary>
        public static ReservationCommandResult Success(string message) => new()
        {
            IsSuccess = true,
            SuccessMessage = message
        };

        /// <summary>建立失敗的結果。</summary>
        public static ReservationCommandResult Fail(string message) => new()
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }
}