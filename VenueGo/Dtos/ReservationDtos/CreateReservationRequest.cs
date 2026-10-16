using System.ComponentModel.DataAnnotations;

namespace VenueGo.Dtos.ReservationDtos
{
    /// <summary>
    /// 建立預約（POST /api/reservations）前端送來的資料。
    /// <para>
    /// 【刻意沒有的欄位】
    /// 金額：由後端依時段重新計算，前端送什麼都不採用（否則任何人都能改成 1 元）。
    /// 會員：由登入的 JWT 決定（ICurrentUserService.UserId），不讓前端指定，避免幫別人亂訂。
    /// </para>
    /// <para>
    /// 【這裡的檢查只擋明顯的格式錯誤】例如沒送時段、人數是 0。
    /// [ApiController] 會自動回 400（前端 http.js 整理成 ValidationFailed）。
    /// 人數上限（場地容納人數）、載具格式、付款規則等商業規則，在 MemberBookingService 檢查。
    /// </para>
    /// </summary>
    public class CreateReservationRequest
    {
        /// <summary>場地。</summary>
        [Range(1, int.MaxValue, ErrorMessage = "請選擇場地。")]
        public int VenueId { get; init; }

        /// <summary>使用日期，JSON 送 "2026-10-20"。</summary>
        public DateOnly BookingDate { get; init; }

        /// <summary>
        /// 時段的開始時間，例如 ["17:00", "18:00"]。
        /// 用字串而不是 TimeOnly：JSON 轉 TimeOnly 時 "17:00"（沒有秒數）不一定能轉，
        /// 由 Service 用 TimeOnly.TryParseExact(..., "HH:mm") 自己轉，格式錯誤時能回清楚的訊息。
        /// </summary>
        [Required(ErrorMessage = "請選擇時段。")]
        [MinLength(1, ErrorMessage = "請選擇時段。")]
        public List<string> SlotTimes { get; init; } = [];

        /// <summary>使用人數。上限（場地容納人數）由 Service 檢查。</summary>
        [Range(1, 999, ErrorMessage = "使用人數至少 1 人。")]
        public int PersonAmount { get; init; }

        /// <summary>
        /// 付款方式（PaymentMethod 列舉的數字）：0 現場、1 線上。
        /// 用 byte：JSON 送 5 這種不存在的值時，列舉型別不會自動報錯，由 Service 用 Enum.IsDefined 檢查。
        /// </summary>
        public byte PaymentMethod { get; init; }

        /// <summary>發票類型（InvoiceType 列舉的數字）：0 紙本、1 手機條碼。</summary>
        public byte InvoiceType { get; init; }

        /// <summary>手機條碼載具。選手機條碼時必填；紙本時就算有送也會被忽略（寫入 NULL）。</summary>
        [StringLength(20)]
        public string? CarrierNo { get; init; }

        /// <summary>是否同意租借條款。一定要是 true。</summary>
        public bool TermsAccepted { get; init; }

        /// <summary>客人同意的條款版本（試算回傳的 termsVersion）。跟設定檔不同時代表條款改版了。</summary>
        [Required(ErrorMessage = "缺少條款版本。")]
        [StringLength(20)]
        public string TermsVersion { get; init; } = string.Empty;
    }
}
