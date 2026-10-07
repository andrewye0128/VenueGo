using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    // 超時費用的現場收款方式
    // PaymentMethod 是線上的方式
    public enum OvertimePaymentMethod : byte
    {
        [Display(Name = "現金")]
        Cash = 1,

        [Display(Name = "刷卡")]
        Card = 2,
    }
}
