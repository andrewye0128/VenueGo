using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    // 超時費用狀態（OvertimeCharge.Status）
    public enum OvertimeChargeStatus : byte
    {
        [Display(Name = "待收")]
        Pending = 1,

        [Display(Name = "已收")]
        Paid = 2,

        [Display(Name = "已免收")]
        Waived = 3,
    }
}

