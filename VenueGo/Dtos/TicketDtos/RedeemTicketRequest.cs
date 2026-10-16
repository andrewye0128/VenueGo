namespace VenueGo.Dtos.TicketDtos
{
    // 領取票券：前台送來的驗證碼
    public class RedeemTicketRequest
    {
        public string Code { get; set; } = null!;
    }
}
