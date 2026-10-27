using VenueGo.Models.PaymentModels;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 收款確認：「錢收到了」之後要做的事，後台標記已付款、綠界付款成功都呼叫這裡。
    /// 同一筆付款呼叫幾次都沒關係，只有第一次會真的修改（重複的通知回傳 AlreadyProcessed）
    /// </summary>
    public interface IPaymentConfirmationService
    {
        Task<PaymentConfirmResult> ConfirmPaidAsync(PaymentConfirmRequest request, CancellationToken cancellationToken = default);
    }
}