using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Models.CheckinModels;
using VenueGo.Models.Enums;

namespace VenueGo.ViewModels.CheckinViewModels
{
    //「異動紀錄」分頁 >> 人工取消/轉失效按鈕 + 異動紀錄
    public class TicketManualTabViewModel : TicketTabViewModelBase
    {
        public List<CTicketStatusLogWrap> Logs { get; set; } = new List<CTicketStatusLogWrap>();

        //兩個 Modal 的原因下拉選項(由 Factory 依動作篩好)
        public List<SelectListItem> CancelReasonOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ExpireReasonOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> ReissueReasonOptions { get; set; } = new List<SelectListItem>();

        //按鈕要不要能按 >> 只有「有效」且還沒超過預約時間的票券可以操作
        public bool CanCancel => !IsPastEndTime && Status == (byte)EntryTicketStatus.Valid;
        public bool CanExpire => CanCancel;

        public bool CanReissue => CanCancel;
    }
}