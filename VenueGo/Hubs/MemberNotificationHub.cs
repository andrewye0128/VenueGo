using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using VenueGo.Constants;

namespace VenueGo.Hubs
{
    /// <summary>
    /// 前台會員的即時通知（SignalR 的「總機」）。
    /// <para>
    /// 會員登入後，前端會連上這裡並保持連線；後端有消息時（例如預約逾期），
    /// 透過 IMemberNotifier 通知那位會員的所有分頁，前端收到後再呼叫 API 讀最新資料。
    /// </para>
    /// <para>
    /// 【只送「有變化」，不送新狀態】通知只帶預約 Id，畫面的資料一律以 API 為準，
    /// 就算有人連上了別人的通知，也拿不到預約內容。
    /// </para>
    /// <para>
    /// 【怎麼知道是哪位會員】SignalR 預設用 ClaimTypes.NameIdentifier 當作使用者 Id，
    /// 跟 JwtService 簽發的 token 一樣，所以 Clients.User("18") 就是會員 18 的所有連線。
    /// </para>
    /// <para>
    /// 目前前端不會呼叫這裡的方法（只有後端往前端送），所以類別裡沒有方法。
    /// </para>
    /// </summary>
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = RoleNames.Member)]
    public class MemberNotificationHub : Hub
    {
        /// <summary>總機的網址。前端連線、JWT 從網址讀 token 的判斷都用這個</summary>
        public const string Path = "/hubs/member-notifications";

        /// <summary>
        /// 事件名稱：會員的某一筆預約狀態變了（目前是逾期）。
        /// 內容：{ reservationId }。前端要用一樣的字串接收。
        /// </summary>
        public const string ReservationChangedEvent = "ReservationChanged";
    }
}