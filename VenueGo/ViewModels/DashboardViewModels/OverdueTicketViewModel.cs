namespace VenueGo.ViewModels.DashboardViewModels
{
    public class OverdueTicketViewModel
    {
        public int TicketId { get; set; }
        public int OrderId { get; set;  }
        public string Qrtoken { get; set; } = string.Empty;
        public string VenueName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateOnly BookingDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        // 超時需要
        public bool IsFree { get; set; }
        public int EstimatedAmount { get; set; }

        public byte? ChargeStatus { get; set; } //排程或有掃描出場
        public int? ChargeAmount { get; set; } //排程或是出場凍結金額

        public DateTime EndAt => BookingDate.ToDateTime(EndTime);
        public TimeSpan OverdueDuration { get; set; }
    }
}
