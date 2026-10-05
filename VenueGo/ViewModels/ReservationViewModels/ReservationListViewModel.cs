using System;
using System.Collections.Generic;
using System.ComponentModel;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.ReservationViewModels
{
    public class ReservationListViewModel
    {
        [DisplayName("預約編號")]
        public int ReservationId { get; set; }

        [DisplayName("會員名稱")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>會員 Email，列表頁「會員」欄位要跟姓名一起顯示。</summary>
        public string UserEmail { get; set; } = string.Empty;

        [DisplayName("場地名稱")]
        public string VenueName { get; set; } = string.Empty;

        [DisplayName("預約日期")]
        public DateOnly BookingDate { get; set; }

        public TimeOnly StartTime { get; set; }

        public TimeOnly EndTime { get; set; }

        [DisplayName("使用時段")]
        public string TimeRange => $"{StartTime:HH\\:mm} ~ {EndTime:HH\\:mm}";

        [DisplayName("預約狀態")]
        public ReservationStatus ReservationStatus { get; set; }

        public string ReservationStatusText => ReservationStatus.GetDisplayName();

        /// <summary>預約狀態徽章的樣式類別，寫法與 ReservationDetailViewModel 一致。</summary>
        public string ReservationStatusBadgeClass => ReservationStatus switch
        {
            ReservationStatus.Pending => "text-bg-warning-subtle text-warning-emphasis",
            ReservationStatus.Confirmed => "text-bg-success-subtle text-success-emphasis",
            ReservationStatus.Completed => "text-bg-primary-subtle text-primary-emphasis",
            _ => "text-bg-secondary-subtle text-secondary-emphasis"
        };

        [DisplayName("付款狀態")]
        public PaymentStatus? PaymentStatus { get; set; }

        public string PaymentStatusText => PaymentStatus?.GetDisplayName() ?? "尚未成立訂單";

        /// <summary>
        /// 付款狀態徽章的樣式類別，寫法與 ReservationDetailViewModel 一致。
        /// 這裡的 PaymentStatus 屬性是 nullable（PaymentStatus?），
        /// switch 比對列舉成員時要用完整命名空間路徑，
        /// 不然編譯器會把 PaymentStatus.Unpaid 誤判成「去屬性本身找一個叫 Unpaid 的成員」
        /// 而不是「列舉型別 PaymentStatus 的 Unpaid 成員」，導致編譯錯誤。
        /// </summary>
        public string PaymentStatusBadgeClass => PaymentStatus switch
        {
            VenueGo.Models.Enums.PaymentStatus.Unpaid => "text-bg-danger-subtle text-danger-emphasis",
            VenueGo.Models.Enums.PaymentStatus.Paid => "text-bg-success-subtle text-success-emphasis",
            VenueGo.Models.Enums.PaymentStatus.Refunding or VenueGo.Models.Enums.PaymentStatus.Processing
                => "text-bg-primary-subtle text-primary-emphasis",
            _ => "text-bg-secondary-subtle text-secondary-emphasis"
        };

        /// <summary>已收款金額，取消彈窗的退款提醒要用。查無訂單/付款紀錄時為 null。</summary>
        public int? PaidAmount { get; set; }

        /// <summary>已收款金額顯示文字。</summary>
        public string PaidAmountText => $"NT$ {(PaidAmount ?? 0):N0}";

        /// <summary>
        /// 可否取消：預約仍有效即可。呼叫共用規則，跟詳細頁的 CanCancel 是同一條規則。
        /// </summary>
        public bool CanCancel => ReservationStatus.IsActive();

        /// <summary>取消時是否需要處理退款：已收款者取消後款項要退回，彈窗要提醒櫃檯。</summary>
        public bool RequiresRefund => PaymentStatus == VenueGo.Models.Enums.PaymentStatus.Paid;

        /// <summary>預約日期顯示文字，含星期。</summary>
        public string BookingDateText
        {
            get
            {
                string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
                return $"{BookingDate:yyyy/MM/dd}（{weekdays[(int)BookingDate.DayOfWeek]}）";
            }
        }
    }

    /// <summary>
    /// 預約列表頁的篩選條件，由查詢字串繫結（例如 ?keyword=王&amp;reservationStatus=0）。
    /// </summary>
    public class ReservationListFilter
    {
        /// <summary>關鍵字，同時比對會員姓名、Email、預約編號。</summary>
        public string? Keyword { get; set; }

        /// <summary>預約日期範圍起（含）。</summary>
        public DateOnly? DateFrom { get; set; }

        /// <summary>預約日期範圍迄（含）。</summary>
        public DateOnly? DateTo { get; set; }

        public ReservationStatus? ReservationStatus { get; set; }

        public PaymentStatus? PaymentStatus { get; set; }
    }

    /// <summary>
    /// 預約列表頁（整頁與局部更新的 Partial 共用同一份 ViewModel）。
    /// </summary>
    public class ReservationListPageViewModel
    {
        public ReservationListFilter Filter { get; set; } = new();

        public IReadOnlyList<ReservationListViewModel> Items { get; set; }
            = Array.Empty<ReservationListViewModel>();
    }
}
