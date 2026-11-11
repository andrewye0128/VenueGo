namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 預約逾期處理：把「超過付款期限、還沒付款」的預約改成已逾期，並釋放時段。
    /// <para>由 ReservationExpiryHostedService 每 10 秒呼叫一次；改成已逾期的預約會通知會員（IMemberNotifier）。</para>
    /// </summary>
    public interface IReservationExpiryService
    {
        /// <summary>
        /// 處理一輪逾期（最多 100 筆）。
        /// </summary>
        /// <returns>這一輪實際改成逾期的筆數。</returns>
        Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
    }
}
