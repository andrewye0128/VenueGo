using Microsoft.AspNetCore.Mvc;
using VenueGo.ViewModels.Reservations;
using VenueGo.Services.Reservations;
 
namespace VenueGo.ViewComponents
{
    /// <summary>
    /// 右側「預約資訊摘要」面板。
    /// <para>
    /// 【為何用 ViewComponent 而不是 Partial View】這個面板在五個步驟中都要出現，
    /// 而它的資料來源是 Session 中的暫存資料。若做成 Partial View，
    /// 五個 Action 都要各自取出暫存資料、組裝摘要、再傳給 View，
    /// 等於同一段程式碼寫五次。
    /// ViewComponent 可以自己注入服務取資料，
    /// 因此每個步驟的 View 只需要一行：
    /// @await Component.InvokeAsync("ReservationSummary", new { currentStep = 1 })
    /// </para>
    /// <para>
    /// 判斷準則：只需要「傳資料進去畫版面」用 Partial View（例如步驟進度條）；
    /// 需要「自己去取資料」則用 ViewComponent。
    /// </para>
    /// </summary>
    public class ReservationSummaryViewComponent : ViewComponent
    {
        private readonly IReservationDraftStore _draftStore;
 
        public ReservationSummaryViewComponent(IReservationDraftStore draftStore)
        {
            _draftStore = draftStore;
        }
 
        /// <param name="currentStep">目前步驟編號 1~5，決定要顯示哪些欄位與提示文字。</param>
        public IViewComponentResult Invoke(int currentStep)
        {
            var draft = _draftStore.Get();
            var viewModel = ReservationSummaryViewModel.FromDraft(draft, currentStep);
            return View(viewModel);
        }
    }
}
 