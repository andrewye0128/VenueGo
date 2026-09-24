using System;
using System.Collections.Generic;
using System.Linq;
using VenueGo.Models.Constants;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 預約詳細頁的 ViewModel。
    /// <para>
    /// 【按鈕的顯示判斷放在這裡，不放在 View】
    /// 「什麼狀態下哪顆按鈕該出現」是業務規則，不是版面問題。
    /// 寫在 View 裡會變成一串難讀的 @@if 條件，而且訂單管理頁要用同一套判斷時
    /// 只能複製一次；放在 ViewModel 的唯讀屬性上，兩邊共用同一份規則。
    /// </para>
    /// </summary>
    public class ReservationDetailViewModel
    {
        // ── 預約主檔 ────────────────────────────────────

        public int ReservationId { get; init; }
        public ReservationStatus ReservationStatus { get; init; }
        public DateOnly BookingDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public DateTime ReservedAt { get; init; }
        public ReservationSource Source { get; init; }
        public string TermsVersion { get; init; } = string.Empty;
        public DateTime TermsAcceptedAt { get; init; }

        // ── 會員 ───────────────────────────────────────

        public int UserId { get; init; }
        public string MemberName { get; init; } = string.Empty;
        public string MemberPhone { get; init; } = string.Empty;
        public string MemberEmail { get; init; } = string.Empty;
        public int MemberCumulativeConsumption { get; init; }

        // ── 場地 ───────────────────────────────────────

        public int VenueId { get; init; }
        public string VenueName { get; init; } = string.Empty;
        public string SportName { get; init; } = string.Empty;
        public string VenueLocation { get; init; } = string.Empty;
        public int? VenueCapacity { get; init; }
        public string? VenuePhotoPath { get; init; }

        // ── 訂單 ───────────────────────────────────────

        public int? OrderId { get; init; }
        public string OrderNo { get; init; } = string.Empty;
        public OrderStatus OrderStatus { get; init; }
        public DateTime? OrderCreatedAt { get; init; }
        public int PersonAmount { get; init; }
        public InvoiceType InvoiceType { get; init; }
        public string? CarrierNo { get; init; }
        public int TotalAmount { get; init; }

        /// <summary>費用明細，一格時段一列（方案 C）。</summary>
        public IReadOnlyList<ReservationDetailLineViewModel> Lines { get; init; }
            = Array.Empty<ReservationDetailLineViewModel>();

        // ── 付款 ───────────────────────────────────────

        public int? PaymentId { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
        public PaymentMethod PaymentMethod { get; init; }
        public PaymentChannel PaymentChannel { get; init; }
        public int PaymentAmount { get; init; }
        public DateTime? PaymentDueAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public string? TransactionNo { get; init; }

        // ── 建立與稽核 ──────────────────────────────────

        /// <summary>建立者姓名。會員自助預約或舊資料未記錄時為 null。</summary>
        public string? CreatedByName { get; init; }

        /// <summary>建立者的員工代碼。</summary>
        public string? CreatedByEmployeeNo { get; init; }

        /// <summary>操作紀錄，依時間新到舊排序。</summary>
        public IReadOnlyList<ReservationAuditLogViewModel> AuditLogs { get; init; }
            = Array.Empty<ReservationAuditLogViewModel>();

        // ── 終止資訊（取消／作廢／場館取消時才有值）─────────

        public string? CancelledByName { get; init; }
        public DateTime? CancelledAt { get; init; }
        public string? CancelReason { get; init; }

        // ── 按鈕顯示判斷 ────────────────────────────────

        /// <summary>
        /// 預約是否仍在有效狀態。
        /// 只有待確認與已確認兩種狀態下，時段才還被佔用、才有後續操作空間。
        /// </summary>
        public bool IsActive =>
            ReservationStatus is ReservationStatus.Pending or ReservationStatus.Confirmed;

        /// <summary>
        /// 是否已被終止（取消、場館取消、作廢）。
        /// 用來決定要不要顯示「終止資訊」那張卡。
        /// </summary>
        public bool IsTerminated =>
            ReservationStatus is ReservationStatus.Cancelled
                or ReservationStatus.CancelledByVenue
                or ReservationStatus.Voided
                or ReservationStatus.Expired;

        /// <summary>可否標記為已付款：尚未收款，且預約仍有效。</summary>
        public bool CanMarkAsPaid => IsActive && PaymentStatus == PaymentStatus.Unpaid;

        /// <summary>可否取消：預約仍有效即可，不論是否已付款。</summary>
        public bool CanCancel => IsActive;

        /// <summary>可否作廢：預約仍有效即可。作廢用於開錯單或測試資料。</summary>
        public bool CanVoid => IsActive;

        /// <summary>
        /// 可否編輯。
        /// <para>
        /// 期中一律為 false，編輯功能尚未實作，按鈕不會出現。
        /// 待實作時改為 IsActive &amp;&amp; PaymentStatus == PaymentStatus.Unpaid：
        /// 已收款的預約改時段會產生金額差額，需先完成補款與退款流程才能開放。
        /// 只改這一個屬性即可，Details.cshtml 不必改動。
        /// </para>
        /// </summary>
        public bool CanEdit => false;

        /// <summary>
        /// 取消時是否需要處理退款。
        /// 已收款者取消後款項要退回，彈出視窗要提醒櫃檯。
        /// </summary>
        public bool RequiresRefund => PaymentStatus == PaymentStatus.Paid;

        // ── 顯示用的格式化屬性 ───────────────────────────

        /// <summary>會員編號。資料庫無此欄位，由 UserId 格式化而來。</summary>
        public string MemberNo => $"M{UserId:D4}";

        /// <summary>使用日期，含星期。</summary>
        public string BookingDateText => FormatDate(BookingDate);

        /// <summary>使用時段，例如「17:00 - 19:00」。</summary>
        public string TimeRangeText => $"{StartTime:HH\\:mm} - {EndTime:HH\\:mm}";

        /// <summary>使用時數。由明細列數得出，與 ReservationSlots 的列數一致。</summary>
        public int TotalHours => Lines.Count;

        /// <summary>使用人數顯示文字，附上場地的可容納人數供對照。</summary>
        public string PersonAmountText => VenueCapacity.HasValue
            ? $"{PersonAmount} 人（可容納 {VenueCapacity} 人）"
            : $"{PersonAmount} 人";

        /// <summary>總金額顯示文字。</summary>
        public string TotalAmountText => $"NT$ {TotalAmount:N0}";

        /// <summary>付款時間顯示文字。</summary>
        public string PaidAtText => PaidAt.HasValue
            ? PaidAt.Value.ToString("yyyy/MM/dd HH:mm")
            : "尚未付款";

        /// <summary>付款期限顯示文字。</summary>
        public string PaymentDueAtText => PaymentDueAt.HasValue
            ? PaymentDueAt.Value.ToString("yyyy/MM/dd HH:mm")
            : "-";

        /// <summary>建立者顯示文字，含員工代碼。</summary>
        public string CreatedByText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CreatedByName)) return "會員自行預約";

                return string.IsNullOrWhiteSpace(CreatedByEmployeeNo)
                    ? CreatedByName
                    : $"{CreatedByName}（{CreatedByEmployeeNo}）";
            }
        }

        /// <summary>條款同意顯示文字。</summary>
        public string TermsText => $"{TermsVersion}（{TermsAcceptedAt:HH:mm} 同意）";

        /// <summary>終止資訊卡的標題，依狀態而不同。</summary>
        public string TerminationTitle => ReservationStatus switch
        {
            ReservationStatus.Cancelled => "取消資訊",
            ReservationStatus.CancelledByVenue => "場館取消資訊",
            ReservationStatus.Voided => "作廢資訊",
            ReservationStatus.Expired => "逾期資訊",
            _ => "終止資訊"
        };

        /// <summary>預約狀態徽章的樣式類別。</summary>
        public string ReservationStatusBadgeClass => ReservationStatus switch
        {
            ReservationStatus.Pending => "text-bg-warning-subtle text-warning-emphasis",
            ReservationStatus.Confirmed => "text-bg-success-subtle text-success-emphasis",
            ReservationStatus.Completed => "text-bg-primary-subtle text-primary-emphasis",
            _ => "text-bg-secondary-subtle text-secondary-emphasis"
        };

        /// <summary>訂單狀態徽章的樣式類別。</summary>
        public string OrderStatusBadgeClass => OrderStatus switch
        {
            OrderStatus.AwaitingPayment => "text-bg-warning-subtle text-warning-emphasis",
            OrderStatus.Paid => "text-bg-success-subtle text-success-emphasis",
            _ => "text-bg-secondary-subtle text-secondary-emphasis"
        };

        /// <summary>付款狀態徽章的樣式類別。</summary>
        public string PaymentStatusBadgeClass => PaymentStatus switch
        {
            PaymentStatus.Unpaid => "text-bg-danger-subtle text-danger-emphasis",
            PaymentStatus.Paid => "text-bg-success-subtle text-success-emphasis",
            PaymentStatus.Refunding or PaymentStatus.Processing
                => "text-bg-primary-subtle text-primary-emphasis",
            _ => "text-bg-secondary-subtle text-secondary-emphasis"
        };

        private static string FormatDate(DateOnly date)
        {
            string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
            return $"{date:yyyy/MM/dd}（{weekdays[(int)date.DayOfWeek]}）";
        }
    }

    /// <summary>
    /// 費用明細的一列，對應一筆 OrdersDetails。
    /// </summary>
    public class ReservationDetailLineViewModel
    {
        /// <summary>時段起始時間。OrdersDetails.SlotTime 為 nullable，舊資料可能沒有。</summary>
        public TimeOnly? SlotTime { get; init; }

        public int UnitPrice { get; init; }
        public int DurationHours { get; init; }
        public int Subtotal { get; init; }

        /// <summary>
        /// 是否為尖峰時段。
        /// <para>
        /// OrdersDetails 沒有存這個欄位，因此由 SlotTime 與目前的
        /// SportTypePriceRules.PeakStartTime 比對得出，僅供畫面標示。
        /// 若計價規則在成交後被調整，這個標籤可能與當時的判斷不同，
        /// 但金額仍以 UnitPrice 的快照為準，不受影響。
        /// </para>
        /// </summary>
        public bool IsPeak { get; init; }

        /// <summary>時段顯示文字。SlotTime 為 null 時顯示替代文字。</summary>
        public string TimeRangeText => SlotTime.HasValue
            ? $"{SlotTime.Value:HH\\:mm} - {SlotTime.Value.AddHours(1):HH\\:mm}"
            : "未記錄時段";

        /// <summary>類型顯示文字。</summary>
        public string PeakText => IsPeak ? "尖峰" : "離峰";

        /// <summary>類型文字的樣式類別。</summary>
        public string PeakTextClass => IsPeak ? "text-warning-emphasis" : "text-secondary";
    }

    /// <summary>
    /// 操作紀錄的一列，對應一筆 AuditLogs。
    /// </summary>
    public class ReservationAuditLogViewModel
    {
        /// <summary>發生時間。</summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>操作者姓名。系統自動執行時為 null。</summary>
        public string? OperatorName { get; init; }

        /// <summary>動作代碼，對應 AuditActions 的常數。</summary>
        public string Action { get; init; } = string.Empty;

        /// <summary>時間顯示文字。</summary>
        public string CreatedAtText => CreatedAt.ToString("yyyy/MM/dd HH:mm");

        /// <summary>
        /// 動作的中文標題。
        /// <para>
        /// AuditLogs 只存英文動作代碼，中文描述在顯示時才組出來。
        /// 這樣改文案不必動資料，也不會出現同一個動作在資料庫裡有兩種中文。
        /// </para>
        /// </summary>
        public string ActionTitle => Action switch
        {
            AuditActions.CreateReservation => "建立預約",
            AuditActions.MarkOrderAsPaid => "付款完成",
            AuditActions.CancelReservation => "取消預約",
            AuditActions.VoidReservation => "作廢預約",
            AuditActions.CancelReservationByVenue => "場館取消",
            AuditActions.UpdateReservation => "編輯預約",
            _ => Action
        };

        /// <summary>
        /// 動作的中文描述，含操作者。
        /// <para>
        /// 操作者是實際按下按鈕的人（多為櫃檯員工），不是預約的會員。
        /// 稽核紀錄的重點就在於記錄「誰做的」，兩者不可混用。
        /// </para>
        /// </summary>
        public string Description
        {
            get
            {
                var who = string.IsNullOrWhiteSpace(OperatorName) ? "系統" : OperatorName;

                return Action switch
                {
                    AuditActions.CreateReservation => $"{who} 建立了此預約。",
                    AuditActions.MarkOrderAsPaid => $"{who} 將訂單標記為已付款。",
                    AuditActions.CancelReservation => $"{who} 取消了此預約，時段已釋放。",
                    AuditActions.VoidReservation => $"{who} 作廢了此預約，不計入營運統計。",
                    AuditActions.CancelReservationByVenue => $"{who} 因館方因素取消此預約。",
                    AuditActions.UpdateReservation => $"{who} 修改了此預約。",
                    _ => $"{who} 執行了 {Action}。"
                };
            }
        }
    }
}