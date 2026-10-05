using VenueGo.Dtos.TicketDtos;

namespace VenueGo.Services.Ticket
{
    public interface IMemberTicketQueryService
    {
        /// <summary>
        /// 取得這位會員的票券清單：包含「我現在持有」與「我訂購但已轉給別人」的票。
        /// 已取消（Cancelled）的票不會回傳。
        /// 依預約日期、開始時間由新到舊排序。
        /// </summary>
        Task<List<MemberTicketDto>> GetMyTicketsAsync(
            int userId, CancellationToken cancellationToken = default);
    }
}
