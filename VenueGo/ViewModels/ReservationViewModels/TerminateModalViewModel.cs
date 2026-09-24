namespace VenueGo.ViewModels.ReservationViewModels
{
    /// <summary>
    /// 取消／作廢彈出視窗的 ViewModel。
    /// <para>
    /// 兩種操作共用同一份版面，用這個 ViewModel 切換文案與送出的 Action。
    /// 用工廠方法建立而非讓 View 自己組字串，好處是文案集中在一處，
    /// 之後要改「取消」的說法只需改這裡。
    /// </para>
    /// </summary>
    public class TerminateModalViewModel
    {
        /// <summary>視窗的 HTML id，供按鈕的 data-bs-target 指向。</summary>
        public string ModalId { get; init; } = string.Empty;

        /// <summary>要送出的 Action 名稱。</summary>
        public string ActionName { get; init; } = string.Empty;

        /// <summary>視窗標題，例如「取消預約」。</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>動作的名詞形式，用於組成「取消原因」「取消紀錄」等文案。</summary>
        public string ActionNoun { get; init; } = string.Empty;

        /// <summary>確認按鈕的文字。</summary>
        public string ConfirmButtonText { get; init; } = string.Empty;

        /// <summary>原因欄位的提示文字。</summary>
        public string ReasonPlaceholder { get; init; } = string.Empty;

        /// <summary>已收款時的退款說明。取消與作廢的退款規則不同。</summary>
        public string RefundHintText { get; init; } = string.Empty;

        public int ReservationId { get; init; }
        public string BookingDateText { get; init; } = string.Empty;
        public string TimeRangeText { get; init; } = string.Empty;
        public bool RequiresRefund { get; init; }
        public string PaidAmountText { get; init; } = string.Empty;

        /// <summary>建立「取消預約」視窗的資料。</summary>
        public static TerminateModalViewModel ForCancel(ReservationDetailViewModel detail) => new()
        {
            ModalId = "cancelReservationModal",
            ActionName = "Cancel",
            Title = "取消預約",
            ActionNoun = "取消",
            ConfirmButtonText = "確認取消",
            ReasonPlaceholder = "例如：會員來電表示臨時有事無法前往",
            RefundHintText = "取消後需另行於櫃檯辦理退款。",
            ReservationId = detail.ReservationId,
            BookingDateText = detail.BookingDateText,
            TimeRangeText = detail.TimeRangeText,
            RequiresRefund = detail.RequiresRefund,
            PaidAmountText = detail.TotalAmountText
        };

        /// <summary>
        /// 建立「作廢預約」視窗的資料。
        /// 作廢代表這筆單本來就不該存在，因此已誤收的款項一律全額退還。
        /// </summary>
        public static TerminateModalViewModel ForVoid(ReservationDetailViewModel detail) => new()
        {
            ModalId = "voidReservationModal",
            ActionName = "Void",
            Title = "作廢預約",
            ActionNoun = "作廢",
            ConfirmButtonText = "確認作廢",
            ReasonPlaceholder = "例如：開單時選錯場地，已另開正確單",
            RefundHintText = "作廢代表此單不應存在，需全額退還，不可扣款。",
            ReservationId = detail.ReservationId,
            BookingDateText = detail.BookingDateText,
            TimeRangeText = detail.TimeRangeText,
            RequiresRefund = detail.RequiresRefund,
            PaidAmountText = detail.TotalAmountText
        };
    }
}