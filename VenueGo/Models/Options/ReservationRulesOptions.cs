using System;

namespace VenueGo.Models.Options
{
    /// <summary>
    /// 預約相關的業務規則設定，對應 appsettings.json 的 ReservationRules 節點。
    /// <para>
    /// 【為何放設定檔】這些數字會因營運決策而改變，而且同一個值會被多處使用
    /// （步驟 3 的日曆產生、步驟 3 的送出驗證、期末前台的日曆與 API 驗證）。
    /// 若寫死在程式裡，改動時很容易漏掉其中一處，
    /// 導致日曆只顯示 30 天、但手改網址送 60 天後的日期卻通得過驗證。
    /// </para>
    /// <para>
    /// 【屬性名稱必須與 JSON 鍵名一致】ASP.NET Core 以名稱自動對應，
    /// 改名時兩邊要一起改，否則會靜默地使用下面的預設值而不報錯。
    /// </para>
    /// </summary>
    public class ReservationRulesOptions
    {
        /// <summary>設定節點名稱。用常數避免在多處重複打字串而打錯。</summary>
        public const string SectionName = "ReservationRules";

        // ── 後台（管理員代客建立）──────────────────────────

        /// <summary>
        /// 後台可補登過去幾天的預約。0 表示禁止補登，只能訂今天或之後。
        /// <para>
        /// 目前為 0。若日後要開放補登，除了改這個數字，還必須一併完成配套：
        /// 日曆上過去日期的視覺區隔、步驟 5 的確認警示、必填補登原因、
        /// 以及期末自動逾期排程要排除補登單（其 PaymentDueAt 落在過去，會被誤判逾期）。
        /// 只改數字而不做配套，比禁止補登更危險。
        /// </para>
        /// </summary>
        public int AdminAllowPastDays { get; set; } = 0;

        /// <summary>後台最多可預約幾天後。含今天，所以 30 表示今天起算共 31 天。</summary>
        public int AdminMaxAdvanceDays { get; set; } = 30;

        // ── 前台（會員自助預約，期末使用）────────────────────

        /// <summary>前台會員最快可預約幾天後。</summary>
        public int MemberMinAdvanceDays { get; set; } = 2;

        /// <summary>前台會員最多可預約幾天後。</summary>
        public int MemberMaxAdvanceDays { get; set; } = 30;

        // ── 時段規則 ────────────────────────────────────

        /// <summary>
        /// 一筆預約最多可選幾個時段（小時）。
        /// 步驟 4 驗證使用，避免一個人把整天包下來。
        /// </summary>
        public int MaxSlotsPerReservation { get; set; } = 4;

        /// <summary>
        /// 是否允許預約「當下正在進行中」的時段。
        /// <para>
        /// false（預設）：現在 19:10，則 19:00-20:00 那格不可選，最早只能選 20:00。
        /// true：允許選取當前這一小時，適合現場臨時入場收費。
        /// </para>
        /// </summary>
        public bool AllowCurrentHourSlot { get; set; } = false;

        // ── 條款 ──────────────────────────────────────

        /// <summary>
        /// 目前的租借條款版本，寫入 Reservations.TermsVersion。
        /// 不要在程式裡直接打字串，否則改版時會有漏改的地方。
        /// </summary>
        public string TermsVersion { get; set; } = "v1.0";

        // ── 便利方法 ───────────────────────────────────

        /// <summary>後台可預約的最早日期。</summary>
        public DateOnly GetAdminMinDate(DateOnly today) => today.AddDays(-AdminAllowPastDays);

        /// <summary>後台可預約的最晚日期。</summary>
        public DateOnly GetAdminMaxDate(DateOnly today) => today.AddDays(AdminMaxAdvanceDays);

        /// <summary>判斷某個日期是否在後台允許的預約範圍內。</summary>
        public bool IsWithinAdminRange(DateOnly date, DateOnly today)
            => date >= GetAdminMinDate(today) && date <= GetAdminMaxDate(today);
    }
}