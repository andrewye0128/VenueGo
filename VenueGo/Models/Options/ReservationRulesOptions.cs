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

        // ── 前台（會員自助預約）────────────────────

        /// <summary>
        /// 前台會員最快可預約幾天後。0 表示可以預約當天。
        /// <para>預設值要跟 appsettings.json 一致（2026-10 營運調整為可以訂當天）。</para>
        /// </summary>
        public int MemberMinAdvanceDays { get; set; } = 0;

        /// <summary>前台會員最多可預約幾天後。</summary>
        public int MemberMaxAdvanceDays { get; set; } = 30;



        // ── 前台付款規則 ───────────────────────────────────
        // 試算 API、建立預約都用下方的 IsMemberOnsitePaymentAllowed、GetMember…DueAt 方法，
        // 不要在別的地方自己算，否則改規則時會漏改。

        /// <summary>
        /// 距離使用日至少幾天，才能選「現場付款」。不到這個天數只能線上付款。
        /// <para>原因：太近的預約如果客人沒來也沒付錢，場地就白白空著。</para>
        /// </summary>
        public int MemberOnsitePaymentMinDaysAhead { get; set; } = 3;

        /// <summary>「現場付款」的期限：使用時段開始前幾天。</summary>
        public int MemberOnsitePaymentDueDaysBeforeUse { get; set; } = 1;

        /// <summary>
        /// 「線上付款」的期限：建立預約後幾分鐘內（但不晚於時段開始時間）。
        /// <para>
        /// 跟後台的 <see cref="PaymentCreationBufferMinutes"/> 分開設定：
        /// 兩邊的營運需求可能不同，只想調整其中一邊時不會影響另一邊。
        /// </para>
        /// </summary>
        public int MemberOnlinePaymentBufferMinutes { get; set; } = 30;

        /// <summary>
        /// 「線上付款」：付款期限前幾分鐘就不能「開始」付款。
        /// <para>
        /// 原因：客人進到綠界付款頁、輸入卡號需要幾分鐘。快到期才開始付款的話，
        /// 可能付到一半預約就逾期了。
        /// </para>
        /// <para>
        /// 讀取這個設定的地方用 IOptionsSnapshot，改 appsettings 之後不用重新啟動網站。
        /// </para>
        /// </summary>
        public int MemberOnlinePaymentStartCutoffMinutes { get; set; } = 5;

        // ── 情境一：後台代客的付款期限規則（開關）──────────────

        /// <summary>
        /// 是否啟用情境一的付款期限算法（建立時間＋緩衝分鐘）。
        /// <para>
        /// false（預設）：維持原本「付款期限＝使用時段開始時間」的算法。
        /// 組員的 appsettings.json 沒有這個值時，一律套用這個預設值，行為不受影響；
        /// 要測試新邏輯，在自己的 appsettings.Local.json 裡把這個值設為 true。
        /// </para>
        /// </summary>
        public bool UseScenario1PaymentRule { get; set; } = false;

        /// <summary>情境一／後台代客的付款緩衝分鐘數：建立時間＋這個分鐘數＝付款期限。</summary>
        public int PaymentCreationBufferMinutes { get; set; } = 30;

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

        /// <summary>前台會員可預約的最早日期。</summary>
        /// <param name="today">今天（請用 ITimeService 的網站時間，開發時才會跟著時光機）。</param>
        public DateOnly GetMemberMinDate(DateOnly today) => today.AddDays(MemberMinAdvanceDays);

        /// <summary>前台會員可預約的最晚日期。</summary>
        /// <param name="today">今天（請用 ITimeService 的網站時間）。</param>
        public DateOnly GetMemberMaxDate(DateOnly today) => today.AddDays(MemberMaxAdvanceDays);

        /// <summary>
        /// 判斷某個日期是否在前台會員可預約的範圍內（含頭尾）。
        /// <para>前台查時段、之後的建立預約都用這個方法，兩邊才會是同一條規則。</para>
        /// </summary>
        /// <param name="date">要判斷的日期。</param>
        /// <param name="today">今天（請用 ITimeService 的網站時間）。</param>
        public bool IsWithinMemberRange(DateOnly date, DateOnly today)
            => date >= GetMemberMinDate(today) && date <= GetMemberMaxDate(today);

        /// <summary>
        /// 前台會員這筆預約能不能選「現場付款」：距離使用日至少 <see cref="MemberOnsitePaymentMinDaysAhead"/> 天。
        /// <para>
        /// 用日期相減（DayNumber），不看幾點：今天 23:59 訂 3 天後，跟今天 00:01 訂 3 天後，結果一樣。
        /// 試算 API 與建立預約都呼叫這個方法，兩邊才會是同一條規則。
        /// </para>
        /// </summary>
        /// <param name="bookingDate">使用日期。</param>
        /// <param name="today">今天（請用 ITimeService 的網站時間）。</param>
        public bool IsMemberOnsitePaymentAllowed(DateOnly bookingDate, DateOnly today)
            => bookingDate.DayNumber - today.DayNumber >= MemberOnsitePaymentMinDaysAhead;



        /// <summary>
        /// 前台會員「線上付款」的付款期限：現在＋緩衝分鐘，但不晚於時段開始時間。
        /// <para>
        /// 例：14:50 訂 15:00 的時段，現在＋30 分鐘是 15:20，已經超過開始時間，期限改成 15:00。
        /// </para>
        /// </summary>
        /// <param name="now">現在時間（請用 ITimeService.Now）。</param>
        /// <param name="slotStart">第一格時段的開始時間（日期＋時間）。</param>
        public DateTime GetMemberOnlinePaymentDueAt(DateTime now, DateTime slotStart)
        {
            var due = now.AddMinutes(MemberOnlinePaymentBufferMinutes);
            return due < slotStart ? due : slotStart;
        }

        /// <summary>
        /// 前台會員「線上付款」最晚可以開始付款的時間：付款期限往前 <see cref="MemberOnlinePaymentStartCutoffMinutes"/> 分鐘。
        /// <para>例：期限 15:30 → 15:25 以後就不能按「付款」。</para>
        /// <para>訂單列表、之後的「開始付款」API 都呼叫這個方法，兩邊才會是同一條規則。</para>
        /// </summary>
        /// <param name="dueAt">付款期限（Payments.PaymentDueAt）。</param>
        public DateTime GetMemberOnlinePaymentStartDeadline(DateTime dueAt)
            => dueAt.AddMinutes(-MemberOnlinePaymentStartCutoffMinutes);

        /// <summary>
        /// 前台會員「現場付款」的付款期限：時段開始前 <see cref="MemberOnsitePaymentDueDaysBeforeUse"/> 天。
        /// <para>例：10/20 17:00 的時段 → 10/19 17:00 前到櫃台付款。</para>
        /// </summary>
        /// <param name="slotStart">第一格時段的開始時間（日期＋時間）。</param>
        public DateTime GetMemberOnsitePaymentDueAt(DateTime slotStart)
            => slotStart.AddDays(-MemberOnsitePaymentDueDaysBeforeUse);
    }
}