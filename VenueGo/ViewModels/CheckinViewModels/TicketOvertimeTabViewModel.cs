using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class TicketOvertimeTabViewModel: TicketTabViewModelBase
    {
        public COvertimeChargeWrap? Charge{ get; set; }

        public List<SelectListItem> PaymentOptions = new List<SelectListItem>();

        public bool IsPending => Charge != null && Charge.Status == (byte)OvertimeChargeStatus.Pending;

        //顯示收費表單
        public bool CancheckOut => IsPending && IsInside && Status == (byte)EntryTicketStatus.Expired;

        public string ChargeStatusText => Charge == null ? "" : CheckinDisplayText.OvertimeStatusText(Charge.Status);

        public string PaymentMethodText => Charge == null ? "" : CheckinDisplayText.OvertimePaymentMethodText(Charge.PaymentMethod);
    }
}
