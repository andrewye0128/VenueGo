using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 會員開始線上付款（綠界 AioCheckOut V5）：檢查能不能付、付款改成處理中、產生付款單。
    /// <para>只負責「送出去」。付款有沒有成功由 3-C-4（綠界通知）、3-C-5（主動查詢）決定</para>
    /// </summary>
    public interface IEcpayCheckoutService
    {
        /// <param name="userId">登入的會員（只能付自己的訂單）</param>
        /// <param name="orderId">Orders.OrderId</param>
        /// <param name="cancellationToken">呼叫的地方要取消時用（例如客人關掉頁面）</param>
        Task<StartPaymentResult> StartAsync(int userId, int orderId, CancellationToken cancellationToken = default);
    }
}