using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.ViewModels.Shared;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class TicketIndexViewModel
    {
        public DateOnly Today { get; set; }
        public DateOnly SelectedDate { get; set; }
        public string? Keyword { get; set; }
        public int? SelectedVenueId { get; set; }
        public int? SelectedStatus { get; set; }

        // 
        public List<SelectListItem> AvailableVenues { get; set; } = new();
        // SelectListItem 類別包括: 連線時new一個出來存取
        //public string? Text { get; set; }      // 使用者畫面上看到的文字
        //public string? Value { get; set; }     // 送出表單時實際傳的值
        //public bool Selected { get; set; }     // 是不是預設被選中

        public PagedResult<EntryTicketListViewModel> Tickets { get; set; }
            = PagedResult<EntryTicketListViewModel>.Empty();

        // 分頁列：翻頁時要把目前的篩選條件一起帶著走，不然翻到第 2 頁篩選就消失了
        public PagerViewModel Pager => PagerViewModel.FromPagedResult(
            Tickets, action: "Index", routeValues: BuildFilterRouteValues(), controller: "Ticket");

        // 只放「有填」的條件，網址才不會出現一堆空的 venueId=&status=
        private Dictionary<string, string> BuildFilterRouteValues()
        {
            var values = new Dictionary<string, string>
            {
                ["selectedDate"] = SelectedDate.ToString("yyyy-MM-dd")
            };

            if (!string.IsNullOrWhiteSpace(Keyword))
                values["txtKeyword"] = Keyword;
            if (SelectedVenueId.HasValue)
                values["venueId"] = SelectedVenueId.Value.ToString();
            if (SelectedStatus.HasValue)
                values["status"] = SelectedStatus.Value.ToString();

            return values;
        }
    }

}
