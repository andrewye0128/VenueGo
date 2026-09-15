using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VenueGo.ViewModels.ReservationViewModels;
using VenueGo.Services.Members;
using VenueGo.Services.Reservations;

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
        private readonly IReservationDraftStore _draftStore;

        public ReservationCreateController(
            IMemberQueryService memberQueryService,
            IReservationDraftStore draftStore)
        {
            _memberQueryService = memberQueryService;
            _draftStore = draftStore;
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
            return RedirectToAction(nameof(SelectMember));
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
    }
}
