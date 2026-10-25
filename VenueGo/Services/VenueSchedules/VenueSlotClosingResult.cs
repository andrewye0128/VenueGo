namespace VenueGo.Services.VenueSchedules
{
    //關閉時段的結果 >> IVenueSlotClosingService.CloseSlotAsync 回傳,Controller 依結果設定畫面訊息
    //
    //1. IsSuccess = false >> 時段沒有關閉、預約也沒有取消,Message 是錯誤原因
    //2. IsSuccess = true  >> 時段已關閉(有預約時預約也已取消),Message 是成功訊息
    //3. Warnings          >> 成功但有後續步驟沒做完(取消票券失敗、寄信失敗),要提醒後台人員手動處理
    public class VenueSlotClosingResult
    {
        public bool IsSuccess { get; private init; }

        public string Message { get; private init; } = string.Empty;

        public List<string> Warnings { get; } = new List<string>();

        public static VenueSlotClosingResult Success(string message)
        {
            return new VenueSlotClosingResult { IsSuccess = true, Message = message };
        }

        public static VenueSlotClosingResult Fail(string message)
        {
            return new VenueSlotClosingResult { IsSuccess = false, Message = message };
        }
    }
}
