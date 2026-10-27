namespace VenueGo.Dtos.TicketDtos
{
    // 掃描驗票的結果。不包 ApiResult：「掃到過期 QR」是掃描流程裡的正常結果，
    // 用 Success=false 加白話訊息表達，HTTP 狀態碼仍是 200；只有請求本身有問題才回 400
    public class ScanVerifyResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;           // 給櫃台看的白話訊息

        // 驗到票就帶（不管入場成功或失敗），讓櫃台知道是誰的票；QR 格式錯誤、找不到票時是 null
        public ScanTicketSummaryDto? TicketSummary { get; set; }

        // 離場時發現超時、要先收費：前端顯示黃色卡片和「前往結帳」按鈕
        public bool NeedOvertimeCheckout { get; set; }
        public string? OvertimeCheckoutUrl { get; set; }       // 結帳頁網址，只有 NeedOvertimeCheckout 為 true 才有值
    }
}
