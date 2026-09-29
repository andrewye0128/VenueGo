namespace VenueGo.ViewModels.DashboardViewModels
{
    public class DashboardIndexViewModel
    {
        public List<VenueOccupancyViewModel> VenueOccupancies { get; set; } = new();
        public List<OverdueTicketViewModel> OverdueTickets { get; set; } = new();
    }
}
