using Microsoft.AspNetCore.Mvc.Rendering;

namespace VenueGo.ViewModels.CheckinViewModels
{
    public class TicketIndexViewModel
    {
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

        public List<EntryTicketListViewModel> Tickets { get; set; } = new();
    }

}
