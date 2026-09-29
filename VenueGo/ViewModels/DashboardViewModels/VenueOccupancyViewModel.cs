namespace VenueGo.ViewModels.DashboardViewModels
{
    public class VenueOccupancyViewModel
    {
        public int VenueId { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        public int? Capacity { get; set; }
        public int CurrentCount { get; set; }

        public bool IsOverCapacity => Capacity.HasValue && CurrentCount > Capacity.Value;
        public double? OccupancyRate => Capacity is > 0
            ? Math.Round((double)CurrentCount / Capacity.Value * 100, 0)
            : null;
    }
}
