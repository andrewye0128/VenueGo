namespace VenueGo.Dtos.TicketDtos
{
    // 產生轉讓驗證碼後回給前台的資料
    public class TransferCodeDto
    {
        public string Code { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
