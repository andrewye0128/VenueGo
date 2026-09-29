using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Models.Constants;
using VenueGo.Models.DashboardModels;
using VenueGo.ViewModels.DashboardViewModels;

namespace VenueGo.Controllers
{
    [Authorize(Roles = RoleNames.BackOffice)]
    public class DashboardController : Controller
    {
        private readonly VenueMonitorFactory _venueMonitorFactory;

        public DashboardController(VenueMonitorFactory venueMonitorFactory)
        {
            _venueMonitorFactory = venueMonitorFactory;
        }
        
        public async Task<IActionResult> Index()
        {
            var vm = new DashboardIndexViewModel
            {
                VenueOccupancies = await _venueMonitorFactory.GetVenueOccupancyAsync(),
                OverdueTickets = await _venueMonitorFactory.GetOverdueListAsync()
            };
            return View(vm);
        }
    }
}
