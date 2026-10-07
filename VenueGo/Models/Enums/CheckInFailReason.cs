using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum CheckInFailReason : byte
    {
        [Display(Name = "找不到票券")]
        TicketNotFound = 1,

        [Display(Name = "票券已取消")]
        AlreadyCancelled = 2,

        [Display(Name = "票券已失效")]
        AlreadyExpired = 3,

        [Display(Name = "尚未到預約時間")]
        NotYetStartTime = 4,

        [Display(Name = "進出場順序異常")]
        InvalidSequence = 5,   // 例如還沒入場就要離場、已入場卻再次入場

        [Display(Name = "票券已使用完畢")]
        AlreadyCompleted = 6,
    }
}
