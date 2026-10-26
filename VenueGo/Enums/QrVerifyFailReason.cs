using System.ComponentModel.DataAnnotations;

namespace VenueGo.Enums
{
    public enum QrVerifyFailReason
    {
        [Display(Name="QR 格式錯誤")]
        InvalidFormat = 1,

        [Display(Name="找不到票券")]
        TicketNotFound = 2,

        [Display(Name="簽章錯誤")]
        InvalidSignature = 3,

        [Display(Name="已過期")]
        Expired = 4
    }
}
