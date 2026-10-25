namespace VenueGo.Dtos.TicketDtos
{
    public class ScanVerifyRequest
    {
        // 掃到的 QR 內容字串（v1.票號.時間窗.簽章）
        public string Payload { get; set; } = null!;

        // 櫃台選的模式："in" 入場、"out" 離場。QR 本身不分入場或離場，由櫃台決定
        public string Mode { get; set; } = null!;

        public int VenueId { get; set; }
    }
}
