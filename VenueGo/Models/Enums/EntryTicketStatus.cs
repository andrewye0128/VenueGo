using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum EntryTicketStatus : byte
    {
        [Display(Name = "有效")]
        Valid = 1,      // 有效、未入場

        [Display(Name = "已使用")]
        Used = 2,       // 已入場使用

        [Display(Name = "已失效")]
        Expired = 3,    // 已失效（未使用失效 / 超時失效，原因由入場紀錄判斷）

        [Display(Name = "已取消")]
        Cancelled = 4,  // 已取消（退款）

        [Display(Name = "已完成")]
        Completed = 5,  // 已完成（使用完畢且已離場）
    }
}
