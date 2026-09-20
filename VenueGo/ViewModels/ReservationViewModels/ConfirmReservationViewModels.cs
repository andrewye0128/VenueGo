using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Enums;
using VenueGo.Models.ReservationModels;

namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 步驟 5「確認資料」的表單輸入。
    /// <para>
    /// 只有這幾個欄位是使用者在本步驟填寫的，其餘資料都來自 Session 的暫存。
    /// 金額絕不放在這裡——若由前端傳入，任何人都能改成 1 元。
    /// </para>
    /// </summary>
    public class ConfirmReservationInputModel
    {
        /// <summary>使用人數。上限另由場地的可容納人數驗證。</summary>
        [Display(Name = "使用人數")]
        [Required(ErrorMessage = "請輸入使用人數。")]
        [Range(1, 999, ErrorMessage = "使用人數至少 1 人。")]
        public int PersonAmount { get; set; } = 1;

        /// <summary>發票類型。</summary>
        [Display(Name = "發票類型")]
        public InvoiceType InvoiceType { get; set; } = InvoiceType.Paper;

        /// <summary>
        /// 手機條碼載具。
        /// <para>
        /// RegularExpression 對 null 與空字串不會觸發，因此「選手機條碼時必填」
        /// 這條規則無法只靠屬性完成，必須在服務層另外檢查。
        /// </para>
        /// </summary>
        [Display(Name = "載具號碼")]
        [StringLength(8, ErrorMessage = "載具號碼為斜線加 7 碼，共 8 個字元。")]
        [RegularExpression(@"^/[0-9A-Z+\-.]{7}$",
            ErrorMessage = "載具號碼格式不正確，應為斜線加 7 碼大寫英數字，例如 /AB12345。")]
        public string? CarrierNo { get; set; }

        /// <summary>
        /// 管理員是否已向會員說明並確認同意租借條款。
        /// 未勾選不可建立預約，因為 Reservations.TermsAcceptedAt 為 NOT NULL。
        /// </summary>
        [Display(Name = "租借條款")]
        [Required(ErrorMessage = "請勾選已向會員說明並確認同意租借條款。")]
        public bool TermsAccepted { get; set; }

        /// <summary>
        /// 是否已於現場收款。
        /// 勾選時一併把付款狀態寫為已付款、預約狀態寫為已確認，
        /// 省去建立後再到訂單管理按一次「標記為已付款」。
        /// </summary>
        [Display(Name = "已收款")]
        public bool MarkAsPaid { get; set; }
    }

    /// <summary>
    /// 步驟 5「確認資料」頁面的 ViewModel。
    /// </summary>
    public class ConfirmReservationViewModel
    {
        /// <summary>表單輸入，回填使用者先前填過的值。</summary>
        public ConfirmReservationInputModel Input { get; set; } = new();

        // ── 會員（步驟 1）──────────────────────────────

        public int UserId { get; set; }
        public string? MemberNo { get; set; }
        public string? MemberName { get; set; }
        public string? MemberPhone { get; set; }
        public string? MemberEmail { get; set; }

        // ── 場地（步驟 2）──────────────────────────────

        public string? VenueName { get; set; }
        public string? VenueLocation { get; set; }
        public int? VenueCapacity { get; set; }

        // ── 日期與時段（步驟 3、4）──────────────────────

        public DateOnly? BookingDate { get; set; }

        /// <summary>計價結果，含每一格的明細與總金額。</summary>
        public PricingResult Pricing { get; set; } = PricingResult.Empty();

        // ── 顯示用 ─────────────────────────────────────

        /// <summary>使用日期，含星期。</summary>
        public string BookingDateText
        {
            get
            {
                if (BookingDate is null) return "-";
                string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
                var date = BookingDate.Value;
                return $"{date:yyyy/MM/dd}（{weekdays[(int)date.DayOfWeek]}）";
            }
        }

        /// <summary>使用時段，例如「17:00 - 19:00」。</summary>
        public string TimeRangeText
        {
            get
            {
                if (Pricing.StartTime is null || Pricing.EndTime is null) return "-";
                return $"{Pricing.StartTime.Value:HH\\:mm} - {Pricing.EndTime.Value:HH\\:mm}";
            }
        }

        /// <summary>使用時數。</summary>
        public int TotalHours => Pricing.SlotCount;

        /// <summary>使用人數的上限。場地未設定可容納人數時給一個保守的預設值。</summary>
        public int MaxPersonAmount => VenueCapacity ?? 20;

        /// <summary>
        /// 依單價分組的摘要，例如「離峰 2 小時 NT$ 800」。
        /// <para>
        /// 這個分組只發生在顯示端。寫入資料庫的仍是一格一列的明細，
        /// 因此即使這裡分組有誤，資料庫的金額與總額仍然正確。
        /// </para>
        /// </summary>
        public IReadOnlyList<PriceGroup> PriceGroups => Pricing.GroupByPrice();

        /// <summary>付款方式顯示文字。後台代客建立固定為現場付款。</summary>
        public string PaymentMethodText => PaymentMethod.OnSite.GetDisplayName();

        /// <summary>付款通道顯示文字。</summary>
        public string PaymentChannelText => PaymentChannel.Counter.GetDisplayName();

        /// <summary>條款版本，由設定檔提供。</summary>
        public string TermsVersion { get; set; } = string.Empty;
    }
}