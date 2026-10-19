using Microsoft.AspNetCore.Mvc;
using VenueGo.Helpers;
using VenueGo.Services.ReviewTickets;

namespace VenueGo.Controllers
{
    /// <summary>
    /// 開發期間的手動測試入口。
    ///
    /// ⚠️ 只在 Development 環境可用，其他環境一律回 404。
    ///    沒有掛 [Authorize] 是刻意的——產生密碼雜湊這件事必須在
    ///    「還登不進去」的時候就能做，掛了驗證就變成雞生蛋蛋生雞。
    ///    安全性靠環境判斷把關，不是靠登入。
    ///
    /// ⚠️ 交件或部署前請把整個檔案刪掉。它沒有危害（發佈版本用不了），
    ///    但留著會讓看 code 的人分心。
    ///
    /// 存在的理由：
    ///   1. 產生已知明文密碼的雜湊，讓假資料帳號真的登得進去
    ///   2. CreateReviewPerBookingAsync 目前沒有任何地方會呼叫它
    ///      （那是訂單／付款系統的工作，他們還沒接），
    ///      在他們接上之前，這裡是唯一能驗證它會不會動的地方
    /// </summary>
    public class DevToolsController(
        IWebHostEnvironment env,
        IVisitReviewTicketFactory visitFactory,
        IBookingReviewTicketFactory bookingFactory) : Controller
    {
        private readonly IWebHostEnvironment _env = env;
        private readonly IVisitReviewTicketFactory _visitFactory = visitFactory;
        private readonly IBookingReviewTicketFactory _bookingFactory = bookingFactory;

        /// <summary>回傳 null 代表「是開發環境，繼續」。</summary>
        private IActionResult? RejectIfNotDevelopment()
            => _env.IsDevelopment() ? null : NotFound();

        /// <summary>
        /// 產生密碼雜湊，貼進 seed SQL 的 PasswordHash 欄位用。
        /// 例：/DevTools/Hash?p=Test1234!
        ///
        /// 每次呼叫結果都不一樣，因為 Salt 是隨機的——但都驗得過，
        /// 因為 Salt 就存在回傳字串的中間那一段裡。
        /// </summary>
        [HttpGet]
        public IActionResult Hash(string? p)
        {
            var deny = RejectIfNotDevelopment();
            if (deny != null) return deny;

            if (string.IsNullOrEmpty(p))
                return Content("用法：/DevTools/Hash?p=你要的密碼");

            string hash = PasswordHelper.HashPassword(p);
            bool verified = PasswordHelper.VerifyPassword(p, hash);   // 順手自我檢查

            return Content($"密碼：{p}\r\n雜湊：{hash}\r\n自我驗證：{verified}");
        }

        /// <summary>
        /// 手動觸發「建立現場評論憑證」。
        /// 例：/DevTools/MakeVisitTicket?token=TESTENTRY02
        ///
        /// 前提（10/9 起）：那張 EntryTicket 的狀態必須已經是 Used、有領票的會員（ReceivedUserId），
        /// 而且預約時段內有一筆成功的入場紀錄。
        /// 回 false 不代表壞掉——可能是已經建過了（同一張訂單、同一位會員只有一張），或條件不符。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MakeVisitTicket(string? token)
        {
            var deny = RejectIfNotDevelopment();
            if (deny != null) return deny;

            bool created = await _visitFactory.CreateReviewPerVisitAsync(token);
            return Content($"CreateReviewPerVisitAsync(\"{token}\") => {created}\r\n"
                         + (created ? "已建立現場評論憑證。"
                                    : "沒有建立。可能原因：查無票券／狀態不是 Used or Completed／沒有領票的會員（ReceivedUserId）"
                                      + "／預約時段內沒有成功的入場紀錄／這位會員在這張訂單已經有憑證了"
                                      + "／網站時間還沒到預約時段（10/10 起：請先用時光機撥到時段開始之後，入場紀錄的時間也不能晚於網站時間）。"));
        }

        /// <summary>
        /// 手動觸發「校正實際離場時間」。
        /// 例：/DevTools/RecordEnd?ticketId=9104
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RecordEnd(int? ticketId)
        {
            var deny = RejectIfNotDevelopment();
            if (deny != null) return deny;

            bool updated = await _visitFactory.RecordVisitEndTimeAsync(ticketId);
            return Content($"RecordVisitEndTimeAsync({ticketId}) => {updated}\r\n"
                         + (updated ? "已寫入 ReviewPerVisit.ActualEndTime。"
                                    : "沒有寫入。可能原因：查無票券／沒有領票的會員／查無離場紀錄／離場時間早於租借開始／還沒有評論憑證。"));
        }

        /// <summary>
        /// 手動觸發「建立預約評論憑證」。
        /// 例：/DevTools/MakeBookingTicket?orderId=9102
        ///
        /// 前提：那張訂單要有 PaidAt 不為 null 的付款紀錄。
        /// 這支是訂單／付款系統之後要呼叫的，在他們接上之前用這裡代打。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> MakeBookingTicket(int? orderId)
        {
            var deny = RejectIfNotDevelopment();
            if (deny != null) return deny;

            bool created = await _bookingFactory.CreateReviewPerBookingAsync(orderId);
            return Content($"CreateReviewPerBookingAsync({orderId}) => {created}\r\n"
                         + (created ? $"已建立預約評論憑證，可以用訂購人的帳號登入前台，到 /reviews/booking/{orderId}/write 寫評論了。"
                                    : "沒有建立。可能原因：查無已付款紀錄／已經建過了。"));
        }
    }
}
