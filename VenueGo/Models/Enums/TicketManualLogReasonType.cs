using System.ComponentModel.DataAnnotations;

namespace VenueGo.Models.Enums
{
    public enum TicketManualLogReasonType : byte
    {
        [Display(Name = "客人申請")]
        CustomerRequest = 1,

        [Display(Name = "場館因素")]
        VenueIssue = 2,

        [Display(Name = "重複或誤購")]
        DuplicateOrMistake = 3,

        [Display(Name = "疑似異常")]
        SuspectedAbuse = 4,

        [Display(Name = "系統或資料錯誤")]
        SystemError = 5,

        [Display(Name = "QR 遺失或外流")]
        QrLostOrLeaked = 6,

        [Display(Name = "其他")]
        Other = 7,    // 選這個時備註必填
    }
}
