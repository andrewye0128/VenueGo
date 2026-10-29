using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using VenueGo.Constants;
using VenueGo.Options;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.Payments;

namespace VenueGo.Controllers.Api.PaymentApi
{
    /// <summary>
    /// 接收綠界付款結果的兩個網址（官方文件 2878）。
    /// <para>
    /// 【不用登入】綠界不是會員，沒有 JWT。是不是綠界送的，靠防偽章判斷（在 EcpayNotificationService 裡）
    /// </para>
    /// <para>
    /// Controller 只負責轉接：讀表單、交給 Service、依結果回應。處理規則都在 IEcpayNotificationService
    /// </para>
    /// <para>
    /// 【不接收取消】方法不帶 CancellationToken：收款一旦開始就要做完，對方斷線也不中途停下
    /// </para>
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    // 綠界送來的表單沒有防偽 token。目前全站沒有開防偽檢查，明確寫出來是保險：之後開了也不會擋到綠界
    [IgnoreAntiforgeryToken]
    // 這是給綠界呼叫的，不是給前端用的，不放進 Swagger
    [ApiExplorerSettings(IgnoreApi = true)]
    public class EcpayPaymentApiController : ControllerBase
    {
        /// <summary>導回前台時加上 from=ecpay：前台看到它，就知道要跳出付款結果的視窗</summary>
        private const string FromEcpay = "ecpay";

        private readonly IEcpayNotificationService _notifications;
        private readonly EcpayOptions _ecpay;
        private readonly ILogger<EcpayPaymentApiController> _logger;

        public EcpayPaymentApiController(
            IEcpayNotificationService notifications,
            IOptionsSnapshot<EcpayOptions> ecpay,
            ILogger<EcpayPaymentApiController> logger)
        {
            _notifications = notifications;
            _ecpay = ecpay.Value;
            _logger = logger;
        }

        /// <summary>綠界伺服器通知（ReturnURL）：回 1|OK 表示收到了</summary>
        [HttpPost(EcpayProtocol.Routes.Return)]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> ServerNotify()
        {
            var result = await _notifications.HandleAsync(
                await ReadFieldsAsync(), EcpayNotificationChannel.ServerNotify);

            // 綠界只看是不是「1|OK」，不是的話過 5～15 分鐘再送一次（當天最多 4 次）。
            // 例外不攔：會回 500，綠界一樣會再送
            var body = result.ShouldAcknowledge ? EcpayProtocol.AckResponse : $"0|{result.Outcome}";
            return Content(body, "text/plain");
        }

        /// <summary>瀏覽器帶回（OrderResultURL）：處理完，把客人導回前台的訂單頁</summary>
        [HttpPost(EcpayProtocol.Routes.Result)]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> BrowserReturn()
        {
            string? orderNo = null;
            try
            {
                var result = await _notifications.HandleAsync(
                    await ReadFieldsAsync(), EcpayNotificationChannel.BrowserReturn);
                orderNo = result.OrderNo;
            }
            catch (Exception ex)
            {
                // 處理失敗也要把客人帶回網站，不能停在錯誤頁。付款結果之後會由伺服器通知、排程補上
                _logger.LogError(ex, "處理綠界瀏覽器帶回的付款結果時發生錯誤");
            }

            // 302：瀏覽器會改用 GET 打開前台的頁面。
            // 網址只說「剛從綠界回來」，不帶付款結果：前台要自己向後端查，網址上的東西任何人都能改
            return Redirect(BuildMemberOrdersUrl(orderNo));
        }

        /// <summary>
        /// 綠界送來的表單 → 欄位名稱：值。
        /// 同名的欄位會用逗號接起來，這時防偽章一定對不上，會被當成不能相信
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>> ReadFieldsAsync()
        {
            var form = await Request.ReadFormAsync();
            return form.ToDictionary(field => field.Key, field => field.Value.ToString());
        }

        /// <summary>前台訂單頁的網址；知道是哪一筆時，只列出那一筆，並跳出付款結果的視窗</summary>
        private string BuildMemberOrdersUrl(string? orderNo)
        {
            var url = _ecpay.FrontendBaseUrl.TrimEnd('/') + FrontendRoutes.MemberOrders;
            if (orderNo is null)
            {
                return url;
            }

            return QueryHelpers.AddQueryString(url, new Dictionary<string, string?>
            {
                ["keyword"] = orderNo,   // 同「我的預約」點訂單編號跳過來的做法
                ["from"] = FromEcpay,
            });
        }
    }
}