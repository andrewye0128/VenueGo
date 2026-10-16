using VenueGo.Dtos.BookingDtos;
using VenueGo.Dtos.ReservationDtos;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 前台會員預約服務（前台的「櫃台」）：試算、建立預約。
    /// <para>
    /// 【為何試算與建立放在一起】兩者要做同一套檢查（日期範圍 → 場地／運動／價格 → 時段 → 計價），
    /// 共用同一個檢查方法，才不會發生「試算說可以訂、送出時卻用不同標準被擋」。
    /// </para>
    /// <para>
    /// 【寫入交給誰】檢查通過後整理成 ReservationWriteModel，交給
    /// IReservationCreationService.WriteAsync（後台、前台共用的「廚房」）寫進資料庫。
    /// </para>
    /// </summary>
    public interface IMemberBookingService
    {
        /// <summary>
        /// 試算：時段還能不能訂、金額、能用哪些付款方式。不需要登入，不寫入任何資料。
        /// </summary>
        /// <param name="venueId">場地。</param>
        /// <param name="date">使用日期。</param>
        /// <param name="slotTimes">時段的開始時間（"17:00"），順序不限。</param>
        /// <param name="cancellationToken">使用者離開頁面時取消查詢。</param>
        Task<MemberBookingResult<BookingQuoteDto>> GetQuoteAsync(
            int venueId,
            DateOnly date,
            IReadOnlyList<string> slotTimes,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 建立預約：重新檢查全部規則（不相信試算的結果），通過後寫入資料庫。
        /// </summary>
        /// <param name="userId">登入的會員（由 Controller 從 JWT 取得，不從前端收）。</param>
        /// <param name="request">前端送來的資料（不含金額）。</param>
        /// <param name="cancellationToken">使用者中斷請求時，取消進行中的資料庫操作。</param>
        Task<MemberBookingResult<CreateReservationResultDto>> CreateAsync(
            int userId,
            CreateReservationRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 會員預約服務的回傳結果：成功時有 Data；失敗時有 ErrorCode 與給客人看的 Message。
    /// <para>Controller 依 ErrorCode 決定回 400 還是 409，並包成 ApiResult。</para>
    /// </summary>
    public class MemberBookingResult<T>
    {
        public bool IsSuccess { get; private init; }

        public T? Data { get; private init; }

        /// <summary>錯誤代碼，見 <see cref="BookingErrorCodes"/>；成功時是 null。</summary>
        public string? ErrorCode { get; private init; }

        /// <summary>給客人看的中文訊息；成功時是 null。</summary>
        public string? Message { get; private init; }

        public static MemberBookingResult<T> Ok(T data) => new() { IsSuccess = true, Data = data };

        public static MemberBookingResult<T> Fail(string errorCode, string message)
            => new() { IsSuccess = false, ErrorCode = errorCode, Message = message };
    }

    /// <summary>
    /// 會員預約的錯誤代碼。前端 src/constants/errorCodes.js 有同樣的名稱，改名時兩邊要一起改。
    /// <para>用常數不直接打字串：打錯字時建置就會發現，不會等到前端判斷不到才發現。</para>
    /// </summary>
    public static class BookingErrorCodes
    {
        /// <summary>日期不在會員可預約的範圍（今天～30 天後）。</summary>
        public const string DateOutOfRange = "DateOutOfRange";

        /// <summary>場地不存在／停用、運動停用、沒有啟用中的價格。</summary>
        public const string VenueNotBookable = "VenueNotBookable";

        /// <summary>時段已被訂、已過時間、維護中、不連續、超過上限、公休。</summary>
        public const string SlotUnavailable = "SlotUnavailable";

        /// <summary>檢查時還空著，寫入資料庫的那一刻被別人搶先（唯一鍵衝突）。HTTP 409。</summary>
        public const string SlotTaken = "SlotTaken";

        /// <summary>付款方式不符合規則（例如 2 天內選現場付款）。</summary>
        public const string PaymentMethodNotAllowed = "PaymentMethodNotAllowed";

        /// <summary>沒同意條款，或同意的版本跟目前的版本不同。</summary>
        public const string TermsVersionMismatch = "TermsVersionMismatch";

        /// <summary>填的內容有錯：人數超過容納人數、載具格式錯誤、時段格式錯誤…</summary>
        public const string InvalidInput = "InvalidInput";
    }
}
