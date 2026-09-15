using System;
using System.Linq;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.ViewModels.Reservations
{
    /// <summary>
    /// 右側「預約資訊摘要」面板的 ViewModel。
    /// <para>
    /// 這個面板在五個步驟中都會出現，因此由 ReservationSummaryViewComponent
    /// 統一從 Session 的 <see cref="ReservationDraft"/> 組裝，
    /// 各步驟的 View 只需呼叫一行元件即可，不必各自傳資料。
    /// </para>
    /// <para>
    /// 尚未填寫的欄位一律顯示 <see cref="Placeholder"/>（一個破折號），
    /// 與設計稿一致。
    /// </para>
    /// </summary>
    public class ReservationSummaryViewModel
    {
        /// <summary>尚未填寫時顯示的符號。</summary>
        public const string Placeholder = "-";

        /// <summary>會員顯示文字。</summary>
        public string MemberText { get; init; } = Placeholder;

        /// <summary>場地顯示文字。</summary>
        public string VenueText { get; init; } = Placeholder;

        /// <summary>日期顯示文字，含星期，例如 2026/09/18（五）。</summary>
        public string DateText { get; init; } = Placeholder;

        /// <summary>時段顯示文字，例如 19:00 - 22:00。</summary>
        public string SlotText { get; init; } = Placeholder;

        /// <summary>使用人數顯示文字。未進入步驟 5 前不顯示。</summary>
        public string? PersonAmountText { get; init; }

        /// <summary>預估金額顯示文字，例如 NT$ 1,500。</summary>
        public string AmountText { get; init; } = Placeholder;

        /// <summary>付款方式顯示文字。</summary>
        public string PaymentMethodText { get; init; } = Placeholder;

        /// <summary>付款狀態顯示文字。新增流程中固定為未付款。</summary>
        public string PaymentStatusText { get; init; }
            = PaymentStatus.Unpaid.GetDisplayName();

        /// <summary>發票類型顯示文字。未進入步驟 5 前不顯示。</summary>
        public string? InvoiceTypeText { get; init; }

        /// <summary>載具號碼顯示文字。未填寫時不顯示。</summary>
        public string? CarrierNoText { get; init; }

        /// <summary>面板底部的提示文字，依目前步驟而不同。</summary>
        public string HintText { get; init; } = string.Empty;

        /// <summary>
        /// 由暫存資料與目前步驟組裝摘要。
        /// </summary>
        /// <param name="draft">Session 中的暫存資料。</param>
        /// <param name="currentStep">目前步驟編號 1~5。</param>
        public static ReservationSummaryViewModel FromDraft(ReservationDraft draft, int currentStep)
        {
            return new ReservationSummaryViewModel
            {
                MemberText = draft.IsMemberSelected
                    ? draft.MemberName ?? Placeholder
                    : Placeholder,

                VenueText = draft.IsVenueSelected
                    ? draft.VenueName ?? Placeholder
                    : Placeholder,

                DateText = draft.BookingDate.HasValue
                    ? FormatDate(draft.BookingDate.Value)
                    : Placeholder,

                SlotText = FormatSlots(draft),

                PersonAmountText = currentStep >= 5
                    ? $"{draft.PersonAmount} 人"
                    : null,

                AmountText = draft.EstimatedAmount > 0
                    ? $"NT$ {draft.EstimatedAmount:N0}"
                    : (draft.IsVenueSelected ? "NT$ 0" : Placeholder),

                PaymentMethodText = draft.IsSlotSelected
                    ? draft.PaymentMethod.GetDisplayName()
                    : Placeholder,

                InvoiceTypeText = currentStep >= 5
                    ? draft.InvoiceType.GetDisplayName()
                    : null,

                CarrierNoText = string.IsNullOrWhiteSpace(draft.CarrierNo)
                    ? null
                    : draft.CarrierNo,

                HintText = BuildHint(currentStep)
            };
        }

        private static string FormatDate(DateOnly date)
        {
            string[] weekdays = { "日", "一", "二", "三", "四", "五", "六" };
            return $"{date:yyyy/MM/dd}（{weekdays[(int)date.DayOfWeek]}）";
        }

        private static string FormatSlots(ReservationDraft draft)
        {
            if (draft.SlotTimes.Count == 0) return Placeholder;

            var start = draft.SlotTimes.Min();
            var end = draft.SlotTimes.Max().AddHours(1);
            return $"{start:HH\\:mm} - {end:HH\\:mm}";
        }

        private static string BuildHint(int currentStep) => currentStep switch
        {
            1 => "請依序完成左側步驟，以建立新的預約。",
            2 => "請先選擇場地，再繼續下一步設定日期與時段。",
            3 => "已選擇場地，請選擇要使用的日期。",
            4 => "已選擇使用日期，請選擇預約時段以完成預約。",
            5 => "請確認資料無誤後，點擊「建立預約」完成新增。",
            _ => string.Empty
        };
    }
}