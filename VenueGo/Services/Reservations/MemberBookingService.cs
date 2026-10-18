using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Dtos.BookingDtos;
using VenueGo.Dtos.ReservationDtos;
using VenueGo.Enums;
using VenueGo.Models.Options;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 前台會員預約服務的實作。說明見 <see cref="IMemberBookingService"/>。
    /// </summary>
    public class MemberBookingService : IMemberBookingService
    {
        private readonly dbVenueContext _db;
        private readonly ISlotSelectionValidator _slotValidator;
        private readonly IReservationPricingService _pricingService;
        private readonly IReservationCreationService _creationService;
        private readonly ITimeService _timeService;
        private readonly ReservationRulesOptions _rules;

        public MemberBookingService(
            dbVenueContext db,
            ISlotSelectionValidator slotValidator,
            IReservationPricingService pricingService,
            IReservationCreationService creationService,
            ITimeService timeService,
            IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _db = db;
            _slotValidator = slotValidator;
            _pricingService = pricingService;
            _creationService = creationService;
            _timeService = timeService;
            _rules = rules.Value;
        }

        // ══ 試算 ═══════════════════════════════════════════

        public async Task<MemberBookingResult<BookingQuoteDto>> GetQuoteAsync(
            int venueId,
            DateOnly date,
            IReadOnlyList<string> slotTimes,
            CancellationToken cancellationToken = default)
        {
            var check = await CheckAsync(venueId, date, slotTimes, cancellationToken);
            if (!check.IsSuccess)
            {
                return MemberBookingResult<BookingQuoteDto>.Fail(check.ErrorCode!, check.Message!);
            }

            var booking = check.Data!;
            var options = BuildPaymentOptions(booking, _timeService.Now);

            return MemberBookingResult<BookingQuoteDto>.Ok(new BookingQuoteDto
            {
                VenueId = booking.Venue.VenueId,
                VenueName = booking.Venue.VenueName,
                SportTypeId = booking.Venue.SportTypeId,
                SportName = booking.Venue.SportName,
                Capacity = booking.Venue.Capacity,
                BookingDate = booking.Date,
                StartTime = ToHhMm(booking.Pricing.StartTime!.Value),
                EndTime = ToHhMm(booking.Pricing.EndTime!.Value),
                Slots = booking.Slots
                    .Select(s => new QuoteSlotDto
                    {
                        // FormValue 是 "17:00"，不要用 SlotTime（TimeOnly 會變成 "17:00:00"）
                        SlotTime = s.FormValue,
                        UnitPrice = s.UnitPrice!.Value,   // CheckAsync 已確認每一格都有價格
                        IsPeak = s.IsPeak,
                    })
                    .ToList(),
                TotalAmount = booking.Pricing.TotalAmount,
                PaymentOptions = options
                    .Select(o => new PaymentOptionDto
                    {
                        PaymentMethod = (byte)o.Method,
                        IsAvailable = o.IsAvailable,
                        DueAt = o.DueAt,
                        UnavailableReason = o.UnavailableReason,
                    })
                    .ToList(),
                TermsVersion = _rules.TermsVersion,
            });
        }

        // ══ 建立預約 ═════════════════════════════════════════

        public async Task<MemberBookingResult<CreateReservationResultDto>> CreateAsync(
            int userId,
            CreateReservationRequest request,
            CancellationToken cancellationToken = default)
        {
            // ── 1. 跟試算同一套檢查：日期、場地、時段、價格（重新查，不相信試算的結果）──
            var check = await CheckAsync(
                request.VenueId, request.BookingDate, request.SlotTimes, cancellationToken);
            if (!check.IsSuccess)
            {
                return Fail(check.ErrorCode!, check.Message!);
            }
            var booking = check.Data!;

            // ── 2. 客人填的內容 ──
            // 付款方式、發票類型是 byte：JSON 送 5 這種不存在的值時不會自動報錯，這裡自己檢查
            var paymentMethod = (PaymentMethod)request.PaymentMethod;
            var invoiceType = (InvoiceType)request.InvoiceType;
            if (!Enum.IsDefined(paymentMethod) || !Enum.IsDefined(invoiceType))
            {
                return Fail(BookingErrorCodes.InvalidInput, "付款方式或發票類型不正確。");
            }

            // 人數上限：場地沒有設定容納人數（NULL）時不限制，跟後台一樣
            if (booking.Venue.Capacity is int capacity && request.PersonAmount > capacity)
            {
                return Fail(BookingErrorCodes.InvalidInput, $"使用人數不可超過場地可容納人數 {capacity} 人。");
            }

            // 載具：前台客人可能打小寫，先整理（去空白、轉大寫）再檢查；紙本發票一律寫 NULL
            string? carrierNo = null;
            if (invoiceType == InvoiceType.MobileBarcode)
            {
                carrierNo = InvoiceRules.NormalizeCarrierNo(request.CarrierNo);
                if (carrierNo is null)
                {
                    return Fail(BookingErrorCodes.InvalidInput, "選擇手機條碼載具時，載具號碼為必填。");
                }
                if (!InvoiceRules.IsValidCarrierNo(carrierNo))
                {
                    return Fail(BookingErrorCodes.InvalidInput, "載具號碼格式不正確，應為斜線加 7 碼英數字，例如 /AB12345。");
                }
            }

            // ── 3. 付款規則：用「現在」重新算（線上付款的 30 分鐘從建立的這一刻開始算）──
            var now = _timeService.Now;
            var option = BuildPaymentOptions(booking, now).First(o => o.Method == paymentMethod);
            if (!option.IsAvailable)
            {
                return Fail(BookingErrorCodes.PaymentMethodNotAllowed, option.UnavailableReason!);
            }

            // ── 4. 條款：要勾選，而且版本要跟目前的一樣（客人停留期間條款可能改版）──
            if (!request.TermsAccepted || request.TermsVersion != _rules.TermsVersion)
            {
                return Fail(BookingErrorCodes.TermsVersionMismatch, request.TermsAccepted
                    ? "租借條款已更新，請重新閱讀並同意。"
                    : "請先閱讀並同意租借條款。");
            }

            // ── 5. 整理成寫入單，交給共用的寫入（廚房）──
            // 會員自助預約一律：待確認／待付款／未付款；建立者、稽核操作者都是會員本人
            var model = new ReservationWriteModel
            {
                UserId = userId,
                VenueId = booking.Venue.VenueId,
                BookingDate = booking.Date,
                Pricing = booking.Pricing,

                PersonAmount = request.PersonAmount,
                InvoiceType = invoiceType,
                CarrierNo = carrierNo,

                // 線上付款先固定信用卡（階段 3 的模擬付款頁）；現場付款是櫃台
                PaymentMethod = paymentMethod,
                PaymentChannel = paymentMethod == PaymentMethod.Online
                    ? PaymentChannel.CreditCard
                    : PaymentChannel.Counter,
                PaymentDueAt = option.DueAt!.Value,
                PaidAt = null,

                ReservationStatus = ReservationStatus.Pending,
                OrderStatus = OrderStatus.AwaitingPayment,
                PaymentStatus = PaymentStatus.Unpaid,

                Source = ReservationSource.Web,
                OperatorUserId = userId,
                TermsVersion = _rules.TermsVersion,
                Now = now,
            };

            var result = await _creationService.WriteAsync(model, cancellationToken);
            if (!result.IsSuccess)
            {
                // 寫入那一刻時段被搶走 → SlotTaken（409）；其他（例如系統忙碌）→ 照訊息回傳
                return result.IsSlotConflict
                    ? Fail(BookingErrorCodes.SlotTaken, string.Join(" ", result.Errors))
                    : Fail(BookingErrorCodes.InvalidInput, string.Join(" ", result.Errors));
            }

            return MemberBookingResult<CreateReservationResultDto>.Ok(new CreateReservationResultDto
            {
                ReservationId = result.ReservationId,
                OrderNo = result.OrderNo!,
                VenueName = booking.Venue.VenueName,
                SportName = booking.Venue.SportName,
                BookingDate = booking.Date,
                StartTime = ToHhMm(booking.Pricing.StartTime!.Value),
                EndTime = ToHhMm(booking.Pricing.EndTime!.Value),
                PersonAmount = request.PersonAmount,
                TotalAmount = booking.Pricing.TotalAmount,
                PaymentMethod = (byte)paymentMethod,
                PaymentDueAt = model.PaymentDueAt,
                ReservationStatus = (byte)model.ReservationStatus,
            });

            static MemberBookingResult<CreateReservationResultDto> Fail(string code, string message)
                => MemberBookingResult<CreateReservationResultDto>.Fail(code, message);
        }

        // ══ 共用的檢查（試算、建立預約都用這一個）═══════════════════

        /// <summary>檢查通過後的資料：場地、日期、查證過的時段、計價結果。</summary>
        private sealed record CheckedBooking(
            VenueInfo Venue,
            DateOnly Date,
            IReadOnlyList<TimeSlotStatus> Slots,
            PricingResult Pricing);

        /// <summary>場地與它的運動；IsOpen＝場地與運動都啟用，HasPrice＝運動有啟用中的價格。</summary>
        private sealed record VenueInfo(
            int VenueId,
            string VenueName,
            int SportTypeId,
            string SportName,
            int? Capacity,
            bool IsOpen,
            bool HasPrice);

        /// <summary>
        /// 試算與建立預約共用的檢查，依序：時段格式 → 日期範圍 → 場地／運動／價格 → 時段 → 計價。
        /// <para>先做不用查資料庫的檢查，不通過就不必查資料庫。</para>
        /// </summary>
        private async Task<MemberBookingResult<CheckedBooking>> CheckAsync(
            int venueId,
            DateOnly date,
            IReadOnlyList<string> slotTexts,
            CancellationToken cancellationToken)
        {
            // ── 時段格式："17:00" → TimeOnly ──
            var slotTimes = new List<TimeOnly>();
            foreach (var text in slotTexts)
            {
                if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var time))
                {
                    return CheckFail(BookingErrorCodes.InvalidInput, "時段格式不正確。");
                }
                slotTimes.Add(time);
            }
            if (slotTimes.Count == 0)
            {
                return CheckFail(BookingErrorCodes.InvalidInput, "請至少選擇一個時段。");
            }

            // ── 日期範圍：跟階段 1 查時段用同一條規則 ──
            if (!_rules.IsWithinMemberRange(date, DateOnly.FromDateTime(_timeService.Today)))
            {
                return CheckFail(BookingErrorCodes.DateOutOfRange, "這個日期不開放預約。");
            }

            // ── 場地、運動、價格：一次查完（價格用 Any，SQL 是 EXISTS，不會多查一次）──
            // 條件跟 BookingAvailabilityService 的 IsBookable 一致：場地與運動都啟用、有啟用中的價格
            var venue = await (
                    from v in _db.Venues.AsNoTracking()
                    join s in _db.SportTypes on v.SportTypeId equals s.SportTypeId
                    where v.VenueId == venueId
                    select new VenueInfo(
                        v.VenueId,
                        v.VenueName,
                        v.SportTypeId,
                        s.SportName,
                        v.Capacity,
                        v.IsActive && s.IsActive,
                        _db.SportTypePriceRules.Any(r => r.SportTypeId == v.SportTypeId && r.IsActive)))
                .FirstOrDefaultAsync(cancellationToken);

            if (venue is null || !venue.IsOpen || !venue.HasPrice)
            {
                return CheckFail(BookingErrorCodes.VenueNotBookable, "這個場地目前無法預約，請選擇其他場地。");
            }

            // ── 時段：跟後台用同一個驗證服務（有沒有被訂、連續、最多 4 小時、公休）──
            var validation = await _slotValidator.ValidateAsync(venueId, date, slotTimes, cancellationToken);
            if (!validation.IsValid)
            {
                return CheckFail(BookingErrorCodes.SlotUnavailable, string.Join(" ", validation.Errors));
            }

            // ── 計價：一律由伺服器計算；有任何一格沒有價格就不能訂 ──
            var pricing = _pricingService.Calculate(validation.Slots);
            if (!pricing.IsComplete)
            {
                return CheckFail(BookingErrorCodes.VenueNotBookable, "這個場地目前無法預約，請選擇其他場地。");
            }

            // 依時間排序，試算回傳的 slots 才會是 17:00、18:00 的順序
            var slots = validation.Slots.OrderBy(s => s.SlotTime).ToList();
            return MemberBookingResult<CheckedBooking>.Ok(new CheckedBooking(venue, date, slots, pricing));

            static MemberBookingResult<CheckedBooking> CheckFail(string code, string message)
                => MemberBookingResult<CheckedBooking>.Fail(code, message);
        }

        // ══ 付款方式 ═══════════════════════════════════════

        /// <summary>一種付款方式能不能選、期限、不能選的原因。</summary>
        private sealed record PaymentOption(
            PaymentMethod Method, bool IsAvailable, DateTime? DueAt, string? UnavailableReason);

        /// <summary>
        /// 線上、現場兩種付款方式（線上排前面，任何日期都能選）。
        /// 規則都在 ReservationRulesOptions，這裡只負責組合，不自己算天數或期限。
        /// </summary>
        /// <param name="booking">檢查通過的預約。</param>
        /// <param name="now">現在時間（ITimeService.Now）。</param>
        private List<PaymentOption> BuildPaymentOptions(CheckedBooking booking, DateTime now)
        {
            var slotStart = booking.Date.ToDateTime(booking.Pricing.StartTime!.Value);
            var onsiteAllowed = _rules.IsMemberOnsitePaymentAllowed(booking.Date, DateOnly.FromDateTime(now));

            return
            [
                new(PaymentMethod.Online, true,
                    _rules.GetMemberOnlinePaymentDueAt(now, slotStart), null),
                new(PaymentMethod.OnSite, onsiteAllowed,
                    onsiteAllowed ? _rules.GetMemberOnsitePaymentDueAt(slotStart) : null,
                    onsiteAllowed ? null : $"距離使用日不到 {_rules.MemberOnsitePaymentMinDaysAhead} 天，只能線上付款"),
            ];
        }

        /// <summary>TimeOnly → "17:00"（不要秒數，跟前端的格式一致）。</summary>
        private static string ToHhMm(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
