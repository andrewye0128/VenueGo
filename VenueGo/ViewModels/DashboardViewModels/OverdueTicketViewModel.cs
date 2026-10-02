namespace VenueGo.ViewModels.DashboardViewModels
{
    public class OverdueTicketViewModel
    {
        public int TicketId { get; set; }
        public string Qrtoken { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public DateTime EndAt => BookingDate.ToDateTime(EndTime);
        public TimeSpan OverdueDuration => DateTime.Now - EndAt;
    }
}
