namespace VenueGo.Dtos.TicketDtos
{
    // 掃描結果卡片上顯示的票券資料（不是 QR 內容）
    public class ScanTicketSummaryDto
    {
        public int TicketId { get; set; }
        public string MemberName { get; set; } = null!;   // 持有人
        public int VenueId { get; set; }
        public string VenueName { get; set; } = null!;
        public string Date { get; set; } = null!;
        public string TimeRange { get; set; } = null!;
    }
}