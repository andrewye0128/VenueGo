using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Models.Constants;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.Auth;
using VenueGo.Services.Reservations;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Controllers
{

    //[Authorize(Roles = RoleNames.Member]
    public class ReservationController : Controller
    {
        private readonly dbVenueContext _db;
        private readonly IReservationQueryService _queryService;
        private readonly IReservationCommandService _commandService;
        private readonly ICurrentUserService _currentUser;

        /// <summary>
        /// DbContext 改由建構式注入，不再手動 new。
        /// <para>
        /// 手動 new 有三個問題：物件不會被 Dispose，連線無法即時歸還連線池；
        /// 連線字串得寫死在 DbContext 裡，無法從 appsettings.json 讀取；
        /// 以及無法替換成測試用的資料來源。
        /// </para>
        /// </summary>
        public ReservationController(
            dbVenueContext db,
            IReservationQueryService queryService,
            IReservationCommandService commandService,
            ICurrentUserService currentUser)
        {
            _db = db;
            _queryService = queryService;
            _commandService = commandService;
            _currentUser = currentUser;
        }

        // ══ 列表 ═══════════════════════════════════════

        /// <summary>
        /// 預約列表。
        /// </summary>
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var datas = await (from r in _db.Reservations.AsNoTracking()
                               join u in _db.Users on r.UserId equals u.UserId
                               join v in _db.Venues on r.VenueId equals v.VenueId

                               // 最近的預約排前面，櫃檯多半在查近期的單
                               orderby r.BookingDate descending, r.StartTime descending

                               select new ReservationListViewModel
                               {
                                   ReservationId = r.ReservationId,
                                   UserName = u.Name,
                                   VenueName = v.VenueName,
                                   BookingDate = r.BookingDate,
                                   StartTime = r.StartTime,
                                   EndTime = r.EndTime,
                                   ReservationStatus = r.ReservationStatus,

                                   // 付款狀態改用子查詢，不再用 left join。
                                   //
                                   // 原因一：原本的寫法在 left join 之後又用 o.OrderId 去 join
                                   // Payments，當 o 為 null 時 o.OrderId 會拋 NullReferenceException。
                                   // EF 翻成 SQL 時通常沒事，但改成在記憶體中處理就會出錯。
                                   //
                                   // 原因二：子查詢不會讓主檔的列數被乘開。
                                   // 之後若要加上金額欄位也不必擔心重複列的問題。
                                   PaymentStatus = (from o in _db.Orders
                                                    join p in _db.Payments
                                                        on o.OrderId equals p.OrderId
                                                    where o.ReservationId == r.ReservationId
                                                    select (byte?)p.PaymentStatus)
                                                   .FirstOrDefault()
                               })
                              .ToListAsync(cancellationToken);

            return View(datas);
        }

        // ══ 詳細 ═══════════════════════════════════════

        /// <summary>
        /// 預約詳細頁。
        /// </summary>
        /// <param name="id">預約 Id。</param>
        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var viewModel = await _queryService.GetDetailAsync(id, cancellationToken);

            if (viewModel is null)
            {
                TempData[CDictionary.TK_MSG_找不到指定物件] = "查無此預約，可能已被刪除。";
                return RedirectToAction(nameof(Index));
            }

            return View(viewModel);
        }

        // ══ 狀態異動 ═══════════════════════════════════

        /// <summary>
        /// 標記為已付款。
        /// <para>
        /// 同時更新付款、訂單與預約三個狀態。實際處理在
        /// ReservationCommandService，本方法只負責取得操作者與導向。
        /// </para>
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id, CancellationToken cancellationToken)
        {
            var operatorUserId = _currentUser.UserId;
            if (operatorUserId is null)
            {
                // 未登入或 Cookie 已過期。Challenge 會導向登入頁，
                // 登入成功後自動跳回原本的網址。
                return Challenge();
            }

            var result = await _commandService.MarkAsPaidAsync(
                id, operatorUserId.Value, cancellationToken);

            StoreResultMessage(result);

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// 取消預約（會員因素退訂）。
        /// </summary>
        /// <param name="reason">取消原因，由彈出視窗填寫。</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(
            int id, string reason, CancellationToken cancellationToken)
        {
            var operatorUserId = _currentUser.UserId;
            if (operatorUserId is null)
            {
                return Challenge();
            }

            var result = await _commandService.CancelAsync(
                id, reason, operatorUserId.Value, cancellationToken);

            StoreResultMessage(result);

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// 作廢預約（管理員開錯單或測試資料）。
        /// </summary>
        /// <param name="reason">作廢原因，由彈出視窗填寫。</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Void(
            int id, string reason, CancellationToken cancellationToken)
        {
            var operatorUserId = _currentUser.UserId;
            if (operatorUserId is null)
            {
                return Challenge();
            }

            var result = await _commandService.VoidAsync(
                id, reason, operatorUserId.Value, cancellationToken);

            StoreResultMessage(result);

            return RedirectToAction(nameof(Details), new { id });
        }

        // ══ 共用 ═══════════════════════════════════════

        /// <summary>
        /// 把異動結果的訊息存進 TempData，供重導向後的頁面顯示。
        /// <para>
        /// 三個異動 Action 都要做同一件事，抽成方法避免重複。
        /// 用 TempData 而非 ModelState，是因為 ModelState 在重導向之後就消失了。
        /// </para>
        /// <para>
        /// 異動成功後一律用 RedirectToAction 而非直接回傳 View：
        /// 若回傳 View，網址會停在 POST 的位置，
        /// 使用者按 F5 重新整理時瀏覽器會再次送出表單，造成重複操作。
        /// 這個模式稱為 POST-Redirect-GET。
        /// </para>
        /// </summary>
        private void StoreResultMessage(ReservationCommandResult result)
        {
            if (result.IsSuccess)
            {
                TempData[CDictionary.TK_MSG_操作成功] = result.SuccessMessage;
                return;
            }

            TempData[CDictionary.TK_MSG_操作失敗] = result.ErrorMessage;
        }
    }


    //public class ReservationController : Controller
    //{
    //    /// <summary>
    //    /// 預約管理：列表、詳細、以及狀態異動。
    //    /// <para>
    //    /// 新增預約的五步流程在 ReservationCreateController，不放在這裡：
    //    /// 那個流程有十幾個 Action 與一組 Session 暫存，混在一起會讓本檔案膨脹，
    //    /// 團隊分工時也容易在同一個檔案產生版控衝突。
    //    /// </para>
    //    /// <para>
    //    /// 【權限】整個 Controller 都要求後台角色。
    //    /// 期末開放會員前台後，會員也會拿到驗證 Cookie，
    //    /// 因此不能只寫 [Authorize]，必須限制角色。
    //    /// </para>
    //    /// </summary>
    //    //[Authorize(Roles = RoleNames.BackOffice)]


    //    public IActionResult Index()
    //    {
    //        //dbVenueContext db = new dbVenueContext();
    //        //IEnumerable<Reservation> datas = from t in db.Reservations
    //        //                                 select t;
    //        //return View(datas);

    //        dbVenueContext db = new dbVenueContext();

    //        //List<CReservationWrap> datas = new List<CReservationWrap>();
    //        //foreach (var item in db.Reservations)
    //        //{
    //        //    datas.Add(new CReservationWrap() { reservation = item });
    //        //}

    //        var datas = (from r in db.Reservations
    //                     join u in db.Users on r.UserId equals u.UserId
    //                     join v in db.Venues on r.VenueId equals v.VenueId

    //                     join o in db.Orders on r.ReservationId equals o.ReservationId into og
    //                     from o in og.DefaultIfEmpty()

    //                     join p in db.Payments on o.OrderId equals p.OrderId into pg
    //                     from p in pg.DefaultIfEmpty()

    //                     select new ReservationListViewModel
    //                     {
    //                         ReservationId = r.ReservationId,
    //                         UserName = u.Name,
    //                         VenueName = v.VenueName,
    //                         BookingDate = r.BookingDate,
    //                         StartTime = r.StartTime,
    //                         EndTime = r.EndTime,
    //                         ReservationStatus = r.ReservationStatus,
    //                         PaymentStatus = p == null ? (byte?)null : p.PaymentStatus
    //                     }).ToList();

    //        return View(datas);
    //    }

    //}
}
