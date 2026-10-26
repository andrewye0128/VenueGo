using System.Net;
using VenueGo.Models.ReservationModels;
using VenueGo.Models.VenueModels;
using VenueGo.Services.Reservations;
using VenueGo.Services.Ticket;
using VenueGo.Services.Time;

namespace VenueGo.Services.VenueSchedules
{
    //關閉時段服務的實作 >> 介面說明見 IVenueSlotClosingService
    //
    //寫法說明:
    //1. 不自己改預約、訂單、付款資料,一律呼叫組員已寫好的服務:
    //   - 場館取消 >> IReservationCommandService.CancelByVenueAsync(宗豪的取消流程,含釋放時段、訂單、付款、稽核)
    //   - 取消票券 >> IEntryTicketService.CancelByOrderAsync(宇倫,只處理有效的票券,重複呼叫只會回傳 0)
    //   - 寄信     >> IEmailService.SendEmailAsync(博彥,SMTP 設定放在 appsettings.Local.json)
    //2. 順序固定為:取消預約 >> 新增不開放時段 >> 取消票券 >> 寄信
    //   - 取消預約失敗就整個停下來,什麼都不改,不用回頭刪除剛新增的時段
    //   - 取消預約成功後,資料庫交易已經提交、無法退回,
    //     所以後面的取消票券、寄信失敗都不擋,只寫 log 並回傳警告,提醒後台人員手動處理
    //3. 不接收 CancellationToken:流程做到一半時,後台人員關閉頁面也要把剩下的步驟做完,
    //   避免出現「預約已取消、時段卻沒關閉、會員也沒收到信」的情況
    public class VenueSlotClosingService : IVenueSlotClosingService
    {
        private readonly IReservationCommandService _reservationCommandService;
        private readonly IEntryTicketService _entryTicketService;
        private readonly IEmailService _emailService;
        private readonly ITimeService _timeService;
        private readonly ILogger<VenueSlotClosingService> _logger;

        //不開放時段的查詢、新增,沿用場地模組的 Factory(跟管理頁同一份)
        private readonly CVenueUnavailableSlotFactory _venueUnavailableSlotFactory = new CVenueUnavailableSlotFactory();

        public VenueSlotClosingService(
            IReservationCommandService reservationCommandService,
            IEntryTicketService entryTicketService,
            IEmailService emailService,
            ITimeService timeService,
            ILogger<VenueSlotClosingService> logger)
        {
            _reservationCommandService = reservationCommandService;
            _entryTicketService = entryTicketService;
            _emailService = emailService;
            _timeService = timeService;
            _logger = logger;
        }


        /***** 對外方法 *****/
        public async Task<VenueSlotClosingResult> CloseSlotAsync(
            int venueId, DateOnly date, TimeOnly time, string reason, int operatorUserId)
        {
            string timeText = time.ToString("HH:mm");

            //1. 後端重新查這個時段有沒有預約,不信任前端按鈕上的 data-booked
            CVenueSlotBooking? booking = _venueUnavailableSlotFactory
                .QueryBookingsByVenueAndDate(venueId, date)
                .FirstOrDefault(b => b.SlotTime == time);

            //2. 沒有預約 >> 直接新增不開放時段
            if (booking == null)
            {
                CreateUnavailableSlot(venueId, date, time, reason, operatorUserId);
                return VenueSlotClosingResult.Success($"已關閉 {timeText} 時段。");
            }

            //3. 有預約 >> 先場館取消整筆預約(一筆預約跨多個時段時,其他時段也會一起釋放)
            //   取消失敗(預約已結束、已取消、原因太長⋯)或發生例外 >> 停下來,時段不關閉
            ReservationCommandResult cancelResult;
            try
            {
                cancelResult = await _reservationCommandService.CancelByVenueAsync(
                    booking.ReservationId, reason, operatorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "場館取消預約失敗,ReservationId {ReservationId}", booking.ReservationId);
                return VenueSlotClosingResult.Fail("取消預約時發生錯誤,時段沒有關閉,請稍後再試。");
            }

            if (!cancelResult.IsSuccess)
            {
                return VenueSlotClosingResult.Fail($"時段沒有關閉：{cancelResult.ErrorMessage}");
            }

            //預約已取消,後面每一步的結果都先收集起來,最後一起組成回傳結果
            //成功訊息直接用取消流程回傳的訊息(含已收款時的退款提醒),之後串金流改了訊息也不用跟著改
            string bookingNo = booking.OrderNo ?? $"R{booking.ReservationId}";
            string message = $"已關閉 {timeText} 時段，並取消預約 {bookingNo}（{booking.MemberName}）。{cancelResult.SuccessMessage}";
            List<string> warnings = new List<string>();

            //4. 新增不開放時段
            try
            {
                CreateUnavailableSlot(venueId, date, time, reason, operatorUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "預約已取消但新增不開放時段失敗,VenueId {VenueId} {Date} {Time}", venueId, date, timeText);
                warnings.Add($"預約已取消，但 {timeText} 時段關閉失敗，請重新關閉一次。");
            }

            //5. 取消這筆訂單還有效的票券
            if (booking.OrderId.HasValue)
            {
                try
                {
                    await _entryTicketService.CancelByOrderAsync(booking.OrderId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "預約已取消但取消票券失敗,OrderId {OrderId}", booking.OrderId.Value);
                    warnings.Add($"預約已取消，但票券取消失敗，請到票券管理確認訂單 {bookingNo} 的票券。");
                }
            }

            //6. 寄信通知會員
            try
            {
                await _emailService.SendEmailAsync(
                    booking.MemberEmail, BuildEmailSubject(bookingNo), BuildEmailBody(booking, bookingNo, reason));
                message += $"通知信已寄至 {booking.MemberEmail}。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "預約已取消但通知信寄送失敗,ReservationId {ReservationId}", booking.ReservationId);
                warnings.Add($"通知信寄送失敗，請另外聯絡會員（{booking.MemberEmail}）。");
            }

            VenueSlotClosingResult result = VenueSlotClosingResult.Success(message);
            result.Warnings.AddRange(warnings);
            return result;
        }


        /***** 內部方法 *****/
        //新增一筆不開放時段,建立時間用 ITimeService(跟管理頁其他地方同一個時間來源)
        private void CreateUnavailableSlot(int venueId, DateOnly date, TimeOnly time, string reason, int operatorUserId)
        {
            CVenueUnavailableSlotWrap wrap = new CVenueUnavailableSlotWrap();
            wrap.VenueId = venueId;
            wrap.UnavailableDate = date;
            wrap.UnavailableTime = time;
            wrap.Reason = reason;
            wrap.CreatedAt = _timeService.Now;
            wrap.CreatedBy = operatorUserId;

            _venueUnavailableSlotFactory.Create(wrap);
        }

        private static string BuildEmailSubject(string bookingNo)
        {
            return $"【VenueGo】場地預約取消通知（訂單編號 {bookingNo}）";
        }

        //通知信內文 >> 會員姓名、場地名稱、原因都是資料庫裡的文字,放進 HTML 前一律 HtmlEncode
        //退款說明寫通用說法,之後串金流也不用改
        private static string BuildEmailBody(CVenueSlotBooking booking, string bookingNo, string reason)
        {
            string name = WebUtility.HtmlEncode(booking.MemberName);
            string venueName = WebUtility.HtmlEncode(booking.VenueName);
            string encodedReason = WebUtility.HtmlEncode(reason);
            string encodedBookingNo = WebUtility.HtmlEncode(bookingNo);
            string usageTime = $"{booking.BookingDate:yyyy-MM-dd} {booking.StartTime:HH:mm}–{booking.EndTime:HH:mm}";

            return $"""
                <p>{name} 您好：</p>
                <p>很抱歉，因館方因素，您以下的預約已被取消：</p>
                <table style="border-collapse: collapse;">
                    <tr><td style="padding: 4px 16px 4px 0;">訂單編號</td><td>{encodedBookingNo}</td></tr>
                    <tr><td style="padding: 4px 16px 4px 0;">場地</td><td>{venueName}</td></tr>
                    <tr><td style="padding: 4px 16px 4px 0;">使用時間</td><td>{usageTime}</td></tr>
                    <tr><td style="padding: 4px 16px 4px 0;">取消原因</td><td>{encodedReason}</td></tr>
                </table>
                <p>如已付款，款項將全額退還，詳情請洽客服。<br />如需使用場地，歡迎重新預約其他時段。</p>
                <p>造成不便，敬請見諒。</p>
                <p>VenueGo 運動中心</p>
                """;
        }
    }
}
