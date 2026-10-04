using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    // 人工異動動作
    // 注意：跟 CheckInAction 是不同欄位，數字重疊沒關係
    public enum TicketLogAction : byte
    {
        [Display(Name = "取消票券")]
        Cancel = 1,

        [Display(Name = "轉為失效")]
        Expire = 2,

        [Display(Name = "補發 QR")]
        Reissue = 3,

        [Display(Name = "轉讓")]
        Transfer = 4,
    }

}
