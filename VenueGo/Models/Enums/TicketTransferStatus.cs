using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum TicketTransferStatus : byte
    {
        [Display(Name = "待領取")]
        Pending = 1,

        [Display(Name = "已完成")]
        Completed = 2,

        [Display(Name = "已取消")]
        Cancelled = 3,

        [Display(Name = "已過期")]
        Expired = 4,
    }
}
