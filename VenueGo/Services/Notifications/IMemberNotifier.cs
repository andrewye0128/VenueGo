namespace VenueGo.Services.Notifications
{
    /// <summary>
    /// 通知前台會員「有東西變了」。
    /// <para>
    /// 呼叫的地方（例如逾期排程）只需要知道「通知會員」，不需要知道是用 SignalR 送的。
    /// 以後要通知付款成功、後台取消，也呼叫這裡；換通知方式只要改實作。
    /// </para>
    /// <para>
    /// 【一定要在資料存好之後才呼叫】交易 Commit 成功後再通知，
    /// 不然前端收到通知去讀資料時，可能還是舊的狀態。
    /// </para>
    /// <para>
    /// 【通知失敗不會丟例外】資料已經改好了，通知只是讓畫面更新得快一點，
    /// 失敗只記 log；前端還有「期限＋75 秒自動再讀」（useDueRefresh）當備案。
    /// </para>
    /// </summary>
    public interface IMemberNotifier
    {
        /// <summary>這位會員的某一筆預約狀態變了，請前端重新讀取</summary>
        Task NotifyReservationChangedAsync(int userId, int reservationId, CancellationToken cancellationToken = default);
    }
}