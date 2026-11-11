using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using VenueGo.Hubs;

namespace VenueGo.Services.Notifications
{
    /// <summary>
    /// IMemberNotifier 的實作：透過 SignalR 的 MemberNotificationHub 通知會員。
    /// <para>
    /// IHubContext 是 SignalR 提供的「在 Hub 外面送訊息」的工具，可以在背景排程裡使用。
    /// 會員沒有連線（沒開網站）時，送出去不會有人收到，也不會出錯。
    /// </para>
    /// </summary>
    public class MemberNotifier : IMemberNotifier
    {
        private readonly IHubContext<MemberNotificationHub> _hubContext;
        private readonly ILogger<MemberNotifier> _logger;

        public MemberNotifier(IHubContext<MemberNotificationHub> hubContext, ILogger<MemberNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyReservationChangedAsync(
            int userId, int reservationId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Clients.User：這位會員的所有連線（同時開好幾個分頁，每個分頁都會收到）
                // 使用者 Id 是字串，用 InvariantCulture 轉，不受電腦的地區設定影響
                await _hubContext.Clients
                    .User(userId.ToString(CultureInfo.InvariantCulture))
                    .SendAsync(MemberNotificationHub.ReservationChangedEvent, new { reservationId }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;   // 關站：往上傳，讓背景排程正常結束
            }
            catch (Exception ex)
            {
                // 資料已經存好了，通知失敗不能影響呼叫的地方（說明見 IMemberNotifier）
                _logger.LogWarning(ex, "通知會員 {UserId} 預約 {ReservationId} 有變化失敗", userId, reservationId);
            }
        }
    }
}