using System.ComponentModel.DataAnnotations;

namespace VenueGo.Enums
{
    public enum PaymentMethodOnSite : byte
    {
        [Display(Name = "現金")]
        Cash = 1,

        [Display(Name = "刷卡")]
        CreditCard = 2,

        [Display(Name = "LINE Pay")]
        LinePay = 3
    }
}
