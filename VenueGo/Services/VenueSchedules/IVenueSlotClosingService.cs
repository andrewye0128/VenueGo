namespace VenueGo.Services.VenueSchedules
{
    //關閉時段服務 >> 後台「場地不開放時段管理」把某個時段標記為不開放時使用
    //
    //1. 這個時段沒有預約 >> 直接新增一筆不開放時段
    //2. 這個時段已有預約 >> 先把整筆預約改成場館取消,成功後才新增不開放時段,再取消票券、寄信通知會員
    //
    //恢復開放(刪除不開放時段)不在這裡,維持在 VenueController 直接呼叫 Factory 刪除
    //在 VenueModuleExtensions_F2 註冊為 Scoped,VenueController 透過建構子注入
    public interface IVenueSlotClosingService
    {
        //關閉某場地某天的某個時段
        //呼叫前 Controller 已檢查:不是過去的時段、目前是開放狀態、原因有填、取得到登入者
        //reason         >> 不開放原因,同時當作場館取消的原因寫入 Reservations.CancelReason
        //operatorUserId >> 操作的後台人員,寫入 VenueUnavailableSlots.CreatedBy 與預約的稽核紀錄
        Task<VenueSlotClosingResult> CloseSlotAsync(
            int venueId, DateOnly date, TimeOnly time, string reason, int operatorUserId);
    }
}
