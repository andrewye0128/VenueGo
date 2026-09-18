using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VenueGo.Models.ReservationModels;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.Services.Members;
using VenueGo.Services.Reservations;
using VenueGo.Services.Venues;
using Microsoft.Extensions.Options;
using VenueGo.Models.Options;
using VenueGo.Services.TimeSlots;

namespace VenueGo.Controllers.Reservations
{
    [Route("Reservation/Create")]
    public class ReservationCreateController : Controller
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}

        private readonly IMemberQueryService _memberQueryService;
        private readonly IVenueQueryService _venueQueryService;
        private readonly ITimeSlotService _timeSlotService;
        private readonly ISlotSelectionValidator _slotValidator;
        private readonly IReservationPricingService _pricingService;
        private readonly IReservationDraftStore _draftStore;
        private readonly ReservationRulesOptions _rules;

        public ReservationCreateController(
            IMemberQueryService memberQueryService,
            IVenueQueryService venueQueryService,
            ITimeSlotService timeSlotService,
            ISlotSelectionValidator slotValidator,
            IReservationPricingService pricingService,
            IReservationDraftStore draftStore,
            IOptionsSnapshot<ReservationRulesOptions> rules)
        {
            _memberQueryService = memberQueryService;
            _venueQueryService = venueQueryService;
            _timeSlotService = timeSlotService;
            _slotValidator = slotValidator;
            _pricingService = pricingService;
            _draftStore = draftStore;
            _rules = rules.Value;
        }

        /// <summary>
        /// 流程入口。清除上一次殘留的暫存資料後導向步驟 1。
        /// <para>
        /// 預約列表的「新增預約」按鈕請連到這裡，而不是直接連到 SelectMember，
        /// 否則上一次中途離開的選擇會被帶到新的一筆預約中。
        /// </para>
        /// </summary>
        [HttpGet("")]
        public IActionResult Start()
        {
            _draftStore.Clear();
            return RedirectToAction(nameof(SelectMember));
        }

        /// <summary>
        /// 步驟 1：顯示會員清單。
        /// </summary>
        /// <param name="criteria">搜尋條件，由查詢字串繫結。</param>
        [HttpGet("SelectMember")]
        public async Task<IActionResult> SelectMember(
            [FromQuery] MemberSearchCriteria criteria, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            var viewModel = new SelectMemberViewModel
            {
                Criteria = criteria,
                Members = await _memberQueryService.SearchAsync(criteria, cancellationToken),
                SelectedUserId = draft.UserId
            };

            ViewData["CurrentStep"] = 1;
            return View(viewModel);
        }

        /// <summary>
        /// 步驟 1：確認選擇的會員並前往步驟 2。
        /// </summary>
        /// <param name="userId">選擇的會員 Id。</param>
        /// <param name="criteria">目前的搜尋條件，驗證失敗時要能重繪同一頁清單。</param>
        [HttpPost("SelectMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectMember(
            int? userId,                                 // 使用者勾選了哪個會員
            [FromQuery] MemberSearchCriteria criteria,   //目前的搜尋條件
            CancellationToken cancellationToken)
        {
            if (userId is null)
            {
                ModelState.AddModelError(string.Empty, "請先選擇一位會員，再進行下一步。");
                return await RedisplaySelectMember(criteria, selectedUserId: null, cancellationToken);
            }

            // 即使畫面已把不可選的會員設為 disabled，仍必須在伺服器端再驗證一次。
            // 前端的 disabled 只能防手誤，無法防止有人直接送出偽造的表單。
            var member = await _memberQueryService.GetSelectableMemberAsync(
                userId.Value, cancellationToken);

            if (member is null)
            {
                ModelState.AddModelError(string.Empty,
                    "選擇的會員不存在或目前無法建立預約，請重新選擇。");
                return await RedisplaySelectMember(criteria, userId, cancellationToken);
            }

            var draft = _draftStore.Get();

            // 若改選了不同的會員，後續步驟的選擇不必清除：
            // 場地、日期、時段與會員無關，保留可省去管理員重選的麻煩。
            draft.UserId = member.UserId;
            draft.MemberNo = member.MemberNo;
            draft.MemberName = member.Name;
            draft.MemberPhone = member.Phone;
            draft.MemberEmail = member.Email;
            draft.MemberCarrierNo = member.CarrierNo;

            _draftStore.Save(draft);

            // TODO 步驟 2：由負責場地選擇的人實作 SelectVenue 後，改為導向該 Action，要改成 (RedirectToAction(nameof(SelectVenue)))。
            return RedirectToAction(nameof(SelectVenue));
        }

        /// <summary>
        /// 驗證失敗時重新顯示步驟 1，並保留原本的搜尋條件與選擇。
        /// <para>
        /// 抽成私有方法是為了避免在兩個錯誤分支中重複組裝 ViewModel；
        /// 這類重複往往是「一邊改了、另一邊忘了改」的來源。
        /// </para>
        /// </summary>
        private async Task<IActionResult> RedisplaySelectMember(
            MemberSearchCriteria criteria, int? selectedUserId, CancellationToken cancellationToken)
        {
            var viewModel = new SelectMemberViewModel
            {
                Criteria = criteria,
                Members = await _memberQueryService.SearchAsync(criteria, cancellationToken),
                SelectedUserId = selectedUserId
            };

            ViewData["CurrentStep"] = 1;
            return View(nameof(SelectMember), viewModel);
        }


        /// <summary>
        /// 步驟 2：顯示場地清單。
        /// </summary>
        /// <param name="criteria">搜尋條件，由查詢字串繫結。</param>
        [HttpGet("SelectVenue")]
        public async Task<IActionResult> SelectVenue(
            [FromQuery] VenueSearchCriteria criteria, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            // 防止有人直接打網址跳過步驟 1。
            // 沒有這道檢查，未選會員就能進到步驟 2，
            // 一路走到最後才發現 UserId 是 null。
            if (draft.MaxAllowedStep < 2)
            {
                return RedirectToAction(nameof(SelectMember));
            }

            return View(await BuildSelectVenueViewModel(criteria, draft.VenueId, cancellationToken));
        }

        /// <summary>
        /// 步驟 2：確認選擇的場地並前往步驟 3。
        /// </summary>
        /// <param name="venueId">選擇的場地 Id。</param>
        /// <param name="criteria">目前的搜尋條件，驗證失敗時要能重繪同一頁清單。</param>
        [HttpPost("SelectVenue")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectVenue(
            int? venueId,
            [FromQuery] VenueSearchCriteria criteria,
            CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            if (draft.MaxAllowedStep < 2)
            {
                return RedirectToAction(nameof(SelectMember));
            }

            if (venueId is null)
            {
                ModelState.AddModelError(string.Empty, "請先選擇一個場地，再進行下一步。");
                return View(await BuildSelectVenueViewModel(criteria, null, cancellationToken));
            }

            // 與步驟 1 相同：畫面已把停用場地設為不可選，但仍必須在伺服器端再驗一次。
            var venue = await _venueQueryService.GetSelectableVenueAsync(venueId.Value, cancellationToken);

            if (venue is null)
            {
                ModelState.AddModelError(string.Empty,
                    "選擇的場地不存在或目前停用，請重新選擇。");
                return View(await BuildSelectVenueViewModel(criteria, venueId, cancellationToken));
            }

            // 換了場地就必須清掉已選的時段與金額。
            // 原因：時段的可用性是「場地 + 日期 + 時段」綁在一起的，
            // 舊場地可預約的時段在新場地可能已被別人訂走；
            // 價格也可能因運動類型不同而改變。
            // 日期本身與場地無關，可以保留。
            if (draft.VenueId != venue.VenueId)
            {
                draft.SlotTimes.Clear();
                draft.EstimatedAmount = 0;
            }

            draft.VenueId = venue.VenueId;
            draft.VenueName = venue.VenueName;
            draft.VenueCapacity = venue.Capacity;

            _draftStore.Save(draft);

            // TODO 步驟 3：SelectDate 實作後改為 RedirectToAction(nameof(SelectDate))
            return RedirectToAction(nameof(SelectDate)); ;
        }

        /// <summary>
        /// 組裝步驟 2 的 ViewModel。
        /// <para>
        /// GET 與 POST 的兩個錯誤分支共三處都需要同一份資料，
        /// 抽成方法避免「一處改了、另兩處忘了改」。
        /// </para>
        /// </summary>
        private async Task<SelectVenueViewModel> BuildSelectVenueViewModel(
            VenueSearchCriteria criteria, int? selectedVenueId, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            ViewData["CurrentStep"] = 2;

            return new SelectVenueViewModel
            {
                Criteria = criteria,
                Venues = await _venueQueryService.SearchAsync(criteria, cancellationToken),
                SportTypes = await _venueQueryService.GetSportTypeOptionsAsync(cancellationToken),
                SelectedVenueId = selectedVenueId,
                MemberName = draft.MemberName,
                MemberPhone = draft.MemberPhone
            };
        }



        /// <summary>
        /// 步驟 3：顯示日曆。
        /// </summary>
        /// <param name="year">要顯示的年。未指定時以已選日期或今天所在的月份為準。</param>
        /// <param name="month">要顯示的月。</param>
        [HttpGet("SelectDate")]
        public async Task<IActionResult> SelectDate(
            int? year, int? month, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            if (draft.MaxAllowedStep < 3)
            {
                // 缺會員就回步驟 1，缺場地就回步驟 2，不要一律丟回第一步
                //return RedirectToAction(
                //    draft.IsMemberSelected ? nameof(SelectVenue) : nameof(SelectMember));

                // 缺哪一步就回哪一步，讓管理員少重做幾次。
                return RedirectToAction(ResolveIncompleteStep(draft));
            }

            return View(await BuildSelectDateViewModel(
                year, month, draft.BookingDate, cancellationToken));
        }

        /// <summary>
        /// 步驟 3：確認選擇的日期並前往步驟 4。
        /// </summary>
        /// <param name="date">選擇的日期。</param>
        [HttpPost("SelectDate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectDate(
            DateOnly? date, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            if (draft.MaxAllowedStep < 3)
            {
                //return RedirectToAction(
                //    draft.IsMemberSelected ? nameof(SelectVenue) : nameof(SelectMember));

                // 缺哪一步就回哪一步，讓管理員少重做幾次。
                return RedirectToAction(ResolveIncompleteStep(draft));
            }

            if (date is null)
            {
                ModelState.AddModelError(string.Empty, "請先選擇使用日期，再進行下一步。");
                return View(await BuildSelectDateViewModel(null, null, null, cancellationToken));
            }

            var today = DateOnly.FromDateTime(DateTime.Now);

            // 驗證一：日期必須落在允許範圍內。
            // 畫面上超出範圍的日期已無法點選，但仍必須在伺服器端再驗一次，
            // 因為手改表單就能送出任意日期。
            if (!_rules.IsWithinAdminRange(date.Value, today))
            {
                ModelState.AddModelError(string.Empty,
                    $"可預約日期為 {_rules.GetAdminMinDate(today):yyyy/MM/dd} ～ " +
                    $"{_rules.GetAdminMaxDate(today):yyyy/MM/dd}，請重新選擇。");
                return View(await BuildSelectDateViewModel(
                    date.Value.Year, date.Value.Month, null, cancellationToken));
            }

            // 驗證二：當天必須真的還有可預約的時段。
            // 這裡直接問 TimeSlotService，與日曆使用同一份計算邏輯，
            // 因此不可能出現「日曆說可預約、送出卻被擋下」的矛盾。
            var slots = await _timeSlotService.GetDaySlotsAsync(
                draft.VenueId!.Value, date.Value, cancellationToken);

            if (slots.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "該日期為公休日，無法建立預約。");
                return View(await BuildSelectDateViewModel(
                    date.Value.Year, date.Value.Month, null, cancellationToken));
            }

            if (!slots.Any(s => s.IsSelectable))
            {
                ModelState.AddModelError(string.Empty,
                    "該日期已無可預約時段，請改選其他日期或其他場地。");
                return View(await BuildSelectDateViewModel(
                    date.Value.Year, date.Value.Month, null, cancellationToken));
            }

            // 換了日期就必須清掉已選的時段與金額，理由與換場地相同：
            // 時段的可用性是「場地 + 日期 + 時段」綁在一起的，
            // 9/18 可訂的 19:00 在 9/19 可能已被別人訂走。
            if (draft.BookingDate != date.Value)
            {
                draft.SlotTimes.Clear();
                draft.EstimatedAmount = 0;
            }

            draft.BookingDate = date.Value;
            _draftStore.Save(draft);

            // TODO 步驟 4：SelectSlots 實作後改為 RedirectToAction(nameof(SelectSlots))
            return RedirectToAction(nameof(SelectSlots));
        }

        /// <summary>
        /// 組裝步驟 3 的 ViewModel。
        /// <para>
        /// GET 與 POST 的四個錯誤分支都需要同一份資料，抽成方法避免重複。
        /// </para>
        /// </summary>
        /// <param name="year">要顯示的年，null 表示自動決定。</param>
        /// <param name="month">要顯示的月，null 表示自動決定。</param>
        /// <param name="selectedDate">已選的日期，用於保持選取狀態。</param>
        private async Task<SelectDateViewModel> BuildSelectDateViewModel(
            int? year, int? month, DateOnly? selectedDate, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();
            var today = DateOnly.FromDateTime(DateTime.Now);

            var minDate = _rules.GetAdminMinDate(today);
            var maxDate = _rules.GetAdminMaxDate(today);

            // 決定要顯示哪個月：網址指定的優先，其次是已選日期所在的月，最後是今天。
            var displayMonth = ResolveDisplayMonth(year, month, selectedDate ?? draft.BookingDate, today);

            // 日曆格線會包含鄰月補格，因此查詢範圍要往前後各放寬一週，
            // 否則月初月末那幾格補格日期會拿不到時段統計而顯示空白。
            var firstOfMonth = new DateOnly(displayMonth.Year, displayMonth.Month, 1);
            var queryFrom = firstOfMonth.AddDays(-7);
            var queryTo = firstOfMonth.AddMonths(1).AddDays(6);

            var availability = await _timeSlotService.GetRangeAvailabilityAsync(
                draft.VenueId!.Value, queryFrom, queryTo, cancellationToken);

            ViewData["CurrentStep"] = 3;

            return new SelectDateViewModel
            {
                DisplayMonth = firstOfMonth,
                Calendar = CalendarViewModel.Build(firstOfMonth, availability, minDate, maxDate, today),
                SelectedDate = selectedDate,
                MinDate = minDate,
                MaxDate = maxDate,
                MemberName = draft.MemberName,
                MemberPhone = draft.MemberPhone,
                VenueName = draft.VenueName
            };
        }

        /// <summary>
        /// 決定日曆要顯示哪個月份，並過濾掉不合法的年月參數。
        /// </summary>
        private static DateOnly ResolveDisplayMonth(
            int? year, int? month, DateOnly? fallbackDate, DateOnly today)
        {
            // 手改網址送出 month=13 或 year=0 時不要讓程式拋例外，
            // 直接忽略參數改用預設月份。
            if (year is >= 1 and <= 9999 && month is >= 1 and <= 12)
            {
                return new DateOnly(year.Value, month.Value, 1);
            }

            var basis = fallbackDate ?? today;
            return new DateOnly(basis.Year, basis.Month, 1);
        }


        /// <summary>
        /// 步驟 4：顯示時段表。
        /// </summary>
        [HttpGet("SelectSlots")]
        public async Task<IActionResult> SelectSlots(CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            if (draft.MaxAllowedStep < 4)
            {
                return RedirectToAction(ResolveIncompleteStep(draft));
            }

            return View(await BuildSelectSlotsViewModel(draft.SlotTimes, cancellationToken));
        }

        /// <summary>
        /// 步驟 4：確認所選時段並前往步驟 5。
        /// </summary>
        /// <param name="slotTimes">
        /// 所選時段的起始時間，來自畫面上一組同名的 checkbox。
        /// 格式為 HH:mm，由 ASP.NET Core 自動繫結為 TimeOnly（需 .NET 7 以上）。
        /// </param>
        [HttpPost("SelectSlots")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectSlots(
            List<TimeOnly>? slotTimes, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            if (draft.MaxAllowedStep < 4)
            {
                return RedirectToAction(ResolveIncompleteStep(draft));
            }

            // 伺服器端完整驗證。畫面上的 JavaScript 只能防手誤，
            // 任何人都能改掉限制直接送出任意時段，所以這裡要重新查資料庫驗一次。
            var validation = await _slotValidator.ValidateAsync(
                draft.VenueId!.Value,
                draft.BookingDate!.Value,
                slotTimes ?? new List<TimeOnly>(),
                cancellationToken);

            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                // 重繪時不沿用使用者送來的選取，避免把不合法的組合再顯示回去
                return View(await BuildSelectSlotsViewModel(new List<TimeOnly>(), cancellationToken));
            }

            // 計價一律使用驗證回傳的時段（伺服器端查來的），
            // 絕不接受前端傳入的金額。
            var pricing = _pricingService.Calculate(validation.Slots);

            if (!pricing.IsComplete)
            {
                ModelState.AddModelError(string.Empty,
                    "所選時段尚未設定價格，請先於場地管理設定該運動類型的計價規則。");
                return View(await BuildSelectSlotsViewModel(new List<TimeOnly>(), cancellationToken));
            }

            draft.SlotTimes = pricing.Details.Select(d => d.SlotTime).ToList();
            draft.EstimatedAmount = pricing.TotalAmount;
            _draftStore.Save(draft);

            // TODO 步驟 5：Confirm 實作後改為 RedirectToAction(nameof(Confirm))
            return RedirectToAction(nameof(SelectSlots));
        }

        /// <summary>
        /// 組裝步驟 4 的 ViewModel。
        /// </summary>
        /// <param name="selectedSlotTimes">要顯示為已選的時段。</param>
        private async Task<SelectSlotsViewModel> BuildSelectSlotsViewModel(
            IReadOnlyList<TimeOnly> selectedSlotTimes, CancellationToken cancellationToken)
        {
            var draft = _draftStore.Get();

            var daySlots = await _timeSlotService.GetDaySlotsAsync(
                draft.VenueId!.Value, draft.BookingDate!.Value, cancellationToken);

            var selectedSet = selectedSlotTimes.ToHashSet();

            // 只把仍然可預約的時段視為已選。
            // 使用者停留在頁面期間，原本選的時段可能已被其他管理員訂走，
            // 這時不該再顯示為已選，否則他會以為還訂得到。
            var buttons = daySlots
                .Select(slot => SlotButtonViewModel.FromStatus(
                    slot, slot.IsSelectable && selectedSet.Contains(slot.SlotTime)))
                .ToList();

            var confirmedSlots = daySlots
                .Where(slot => slot.IsSelectable && selectedSet.Contains(slot.SlotTime))
                .ToList();

            ViewData["CurrentStep"] = 4;

            return new SelectSlotsViewModel
            {
                Slots = buttons,
                MaxSlots = _rules.MaxSlotsPerReservation,
                Pricing = _pricingService.Calculate(confirmedSlots),
                MemberName = draft.MemberName,
                MemberPhone = draft.MemberPhone,
                VenueName = draft.VenueName,
                BookingDate = draft.BookingDate
            };
        }


        /// <summary>
        /// 判斷暫存資料缺哪一步，回傳該回到的 Action 名稱。
        /// <para>
        /// 不要一律丟回步驟 1。缺日期就回步驟 3，讓管理員少重做幾次。
        /// </para>
        /// </summary>
        private static string ResolveIncompleteStep(ReservationDraft draft)
        {
            if (!draft.IsMemberSelected) return nameof(SelectMember);
            if (!draft.IsVenueSelected) return nameof(SelectVenue);
            return nameof(SelectDate);
        }


        /// <summary>
        /// 取消整個新增流程，清除暫存並回到預約列表。
        /// </summary>
        [HttpPost("Cancel")]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel()
        {
            _draftStore.Clear();
            return RedirectToAction("Index", "Reservation");
        }
    }
}
