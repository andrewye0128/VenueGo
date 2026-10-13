using System.ComponentModel.DataAnnotations;
using VenueGo.Models.Entities;

namespace VenueGo.Models.CheckinModels
{
    public class COvertimeChargeWrap
    {
        private OvertimeCharge _overtimeCharge;
        public OvertimeCharge overtimeCharge { get { return _overtimeCharge; } set { _overtimeCharge = value; } }
        public COvertimeChargeWrap() { _overtimeCharge = new OvertimeCharge(); }

        [Key]
        public int OvertimeChargeId
        {
            get { return _overtimeCharge.OvertimeChargeId; }
            set { _overtimeCharge.OvertimeChargeId = value; }
        }

        public int OrderId
        {
            get { return _overtimeCharge.OrderId; }
            set { _overtimeCharge.OrderId = value; }
        }

        public int? TicketId
        {
            get { return _overtimeCharge.TicketId; }
            set { _overtimeCharge.TicketId = value; }
        }

        [Display(Name = "超時分鐘")]
        public int OverdueMinutes
        {
            get { return _overtimeCharge.OverdueMinutes; }
            set { _overtimeCharge.OverdueMinutes = value; }
        }

        [Display(Name = "每小時單價")]
        public int UnitPrice
        {
            get { return _overtimeCharge.UnitPrice; }
            set { _overtimeCharge.UnitPrice = value; }
        }

        [Display(Name = "計費時數")]
        public decimal BillingHours
        {
            get { return _overtimeCharge.BillingHours; }
            set { _overtimeCharge.BillingHours = value; }
        }

        [Display(Name = "應收金額")]
        public int ChargeAmount
        {
            get { return _overtimeCharge.ChargeAmount; }
            set { _overtimeCharge.ChargeAmount = value; }
        }

        public byte Status
        {
            get { return _overtimeCharge.Status; }
            set { _overtimeCharge.Status = value; }
        }

        public byte? PaymentMethod
        {
            get { return _overtimeCharge.PaymentMethod; }
            set { _overtimeCharge.PaymentMethod = value; }
        }

        [Display(Name = "備註")]
        public string? Remark
        {
            get { return _overtimeCharge.Remark; }
            set { _overtimeCharge.Remark = value; }
        }

        [Display(Name = "員工編號")]
        public int? OperatorId
        {
            get { return _overtimeCharge.OperatorId; }
            set { _overtimeCharge.OperatorId = value; }
        }

        [Display(Name = "產生時間")]
        public DateTime CreatedAt
        {
            get { return _overtimeCharge.CreatedAt; }
            set { _overtimeCharge.CreatedAt = value; }
        }

        [Display(Name = "確認時間")]
        public DateTime? ConfirmedAt
        {
            get { return _overtimeCharge.ConfirmedAt; }
            set { _overtimeCharge.ConfirmedAt = value; }
        }
    }
}
