using Microsoft.AspNetCore.Mvc.Rendering;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class TicketManualModalViewModel
    {
        public string ModalId { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ConfirmButtonText { get; set; } = string.Empty;
        public string ConfirmButtonClass { get; set; } = string.Empty;
        public string WarningText { get; set; } = string.Empty;

        //彈窗上方顯示的票券摘要
        public int TicketId { get; set; }
        public string TicketText { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;

        public List<SelectListItem> ReasonOptions { get; set; } = new List<SelectListItem>();

        //建立「取消票券」視窗的資料
        public static TicketManualModalViewModel ForCancel(TicketManualTabViewModel tab)
        {
            TicketManualModalViewModel vm = CreateFromTicket(tab);
            vm.ModalId = "cancelTicketModal";
            vm.ActionName = "ManualCancel";
            vm.Title = "取消票券";
            vm.ConfirmButtonText = "確認取消";
            vm.ConfirmButtonClass = "btn-danger";
            vm.WarningText = "取消後無法復原，這張票將無法再使用。";
            vm.ReasonOptions = tab.CancelReasonOptions;
            return vm;
        }

        //建立「轉為失效」視窗的資料
        public static TicketManualModalViewModel ForExpire(TicketManualTabViewModel tab)
        {
            TicketManualModalViewModel vm = CreateFromTicket(tab);
            vm.ModalId = "expireTicketModal";
            vm.ActionName = "ManualExpire";
            vm.Title = "轉為失效";
            vm.ConfirmButtonText = "確認轉失效";
            vm.ConfirmButtonClass = "btn-warning";
            vm.WarningText = "轉為失效後無法復原，這張票將無法再使用。";
            vm.ReasonOptions = tab.ExpireReasonOptions;
            return vm;
        }

        public static TicketManualModalViewModel ForReissue(TicketManualTabViewModel tab)
        {
            TicketManualModalViewModel vm = CreateFromTicket(tab);
            vm.ModalId = "reissueTicketModal";
            vm.ActionName = "ManualReissue";
            vm.Title = "補發QRcode";
            vm.ConfirmButtonText = "確認補發Qrcode";
            vm.ConfirmButtonClass = "btn-warning";
            vm.WarningText = "補發後舊 QRcode 立即失效。";
            vm.ReasonOptions = tab.ReissueReasonOptions;
            return vm;
        }

        //共用私有方法 >> 兩種視窗都一樣的票券摘要
        private static TicketManualModalViewModel CreateFromTicket(TicketManualTabViewModel tab)
        {
            TicketManualModalViewModel vm = new TicketManualModalViewModel();
            vm.TicketId = tab.TicketId;
            vm.TicketText = $"#{tab.TicketId}（{tab.UserName}）";
            vm.VenueName = tab.VenueName;
            vm.TimeText = $"{tab.BookingDate:yyyy-MM-dd}　{tab.TimeRange}";
            vm.StatusText = tab.StatusText;
            return vm;
        }
    }
}
