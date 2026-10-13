using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.Auth;
using VenueGo.Services.Reservations;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Controllers
{
    [EmployeeAuthorize(RoleNames.Admin, RoleNames.Manager, RoleNames.Staff)]
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
        /// 組出預約列表的篩選查詢。
        /// <para>
        /// 抽成共用的私有方法，是因為 Index（整頁）跟 List（AJAX 局部更新用）
        /// 兩個 Action 都要用同一套篩選條件查資料，只是回傳的包裝不一樣——
        /// 寫成兩份查詢邏輯的話，以後新增篩選條件很容易改一邊忘記改另一邊。
        /// </para>
        /// </summary>
        private IQueryable<ReservationListViewModel> BuildFilteredQuery(ReservationListFilter filter)
        {
            var query = from r in _db.Reservations.AsNoTracking()
                        join u in _db.Users on r.UserId equals u.UserId
                        join v in _db.Venues on r.VenueId equals v.VenueId
                        select new { r, u, v };

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();

                // 關鍵字如果剛好是數字，一併比對預約編號；
                // 不是數字就只比對姓名跟 Email，避免 int.Parse 丟例外。
                if (int.TryParse(keyword, out var keywordId))
                {
                    query = query.Where(x => x.r.ReservationId == keywordId
                        || x.u.Name.Contains(keyword) || x.u.Email.Contains(keyword));
                }
                else
                {
                    query = query.Where(x => x.u.Name.Contains(keyword) || x.u.Email.Contains(keyword));
                }
            }

            if (filter.DateFrom.HasValue)
            {
                query = query.Where(x => x.r.BookingDate >= filter.DateFrom.Value);
            }

            if (filter.DateTo.HasValue)
            {
                query = query.Where(x => x.r.BookingDate <= filter.DateTo.Value);
            }

            if (filter.ReservationStatus.HasValue)
            {
                query = query.Where(x => x.r.ReservationStatus == (byte)filter.ReservationStatus.Value);
            }

            // 付款狀態要先投影出來才能篩選：它本來就是子查詢算出來的，
            // 不是 Reservations 表上現成的欄位。
            var withPayment = query.Select(x => new
            {
                x.r,
                x.u,
                x.v,

                // 付款狀態改用子查詢，不再用 left join。
                //
                // 原因一：原本的寫法在 left join 之後又用 o.OrderId 去 join
                // Payments，當 o 為 null 時 o.OrderId 會拋 NullReferenceException。
                // EF 翻成 SQL 時通常沒事，但改成在記憶體中處理就會出錯。
                //
                // 原因二：子查詢不會讓主檔的列數被乘開。
                PaymentStatus = (from o in _db.Orders
                                 join p in _db.Payments
                                     on o.OrderId equals p.OrderId
                                 where o.ReservationId == x.r.ReservationId
                                 select (PaymentStatus?)p.PaymentStatus)
                                .FirstOrDefault(),

                PaidAmount = (from o in _db.Orders
                              join p in _db.Payments
                                  on o.OrderId equals p.OrderId
                              where o.ReservationId == x.r.ReservationId
                              select (int?)p.Amount)
                             .FirstOrDefault()
            });

            if (filter.PaymentStatus.HasValue)
            {
                withPayment = withPayment.Where(x => x.PaymentStatus == filter.PaymentStatus.Value);
            }

            // 最近的預約排前面，櫃檯多半在查近期的單
            return withPayment
                .OrderByDescending(x => x.r.BookingDate)
                .ThenByDescending(x => x.r.StartTime)
                .Select(x => new ReservationListViewModel
                {
                    ReservationId = x.r.ReservationId,
                    UserName = x.u.Name,
                    UserEmail = x.u.Email,
                    VenueName = x.v.VenueName,
                    BookingDate = x.r.BookingDate,
                    StartTime = x.r.StartTime,
                    EndTime = x.r.EndTime,
                    ReservationStatus = (ReservationStatus)x.r.ReservationStatus,
                    PaymentStatus = x.PaymentStatus,
                    PaidAmount = x.PaidAmount
                });
        }

        /// <summary>
        /// 預約列表。第一次進來、按 F5、從書籤打開都走這裡，回傳完整頁面。
        /// </summary>
        public async Task<IActionResult> Index(ReservationListFilter filter, CancellationToken cancellationToken)
        {
            var vm = new ReservationListPageViewModel
            {
                Filter = filter,
                Items = await BuildFilteredQuery(filter).ToListAsync(cancellationToken)
            };

            return View(vm);
        }

        /// <summary>
        /// 只回傳表格那一塊（Partial）。查詢/篩選由 JavaScript 呼叫，換掉畫面上的表格，
        /// 不用整頁重新整理。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List(ReservationListFilter filter, CancellationToken cancellationToken)
        {
            var vm = new ReservationListPageViewModel
            {
                Filter = filter,
                Items = await BuildFilteredQuery(filter).ToListAsync(cancellationToken)
            };

            return PartialView("_ReservationTable", vm);
        }

        // ══ 詳細 ═══════════════════════════════════════

        /// <summary>
        /// 預約詳細頁。
        /// </summary>
        /// <param name="id">預約 Id。</param>
        /// <param name="cancellationToken">使用者中斷請求（例如關閉頁面）時，取消進行中的查詢。</param>
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
        /// <para>
        /// 這個 Action 同時給兩個地方用：預約詳細頁（一般表單送出，整頁跳轉）
        /// 跟預約列表頁（JavaScript 用 AJAX 呼叫，留在原頁、局部更新）。
        /// 兩種情境真正執行取消的商業邏輯（_commandService.CancelAsync）完全共用，
        /// 只有「做完之後要回應什麼」不一樣，靠 X-Requested-With 這個標頭分辨來源——
        /// 瀏覽器一般表單送出不會帶這個標頭，axios 之類的背景請求預設會自動帶上。
        /// </para>
        /// </summary>
        /// <param name="id">預約 Id。</param>
        /// <param name="reason">取消原因，由彈出視窗填寫。</param>
        /// <param name="cancellationToken">使用者中斷請求（例如關閉頁面）時，取消進行中的資料庫操作。</param>
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

            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isAjax)
            {
                return Json(new
                {
                    success = result.IsSuccess,
                    message = result.IsSuccess ? result.SuccessMessage : result.ErrorMessage
                });
            }

            StoreResultMessage(result);

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// 作廢預約（管理員開錯單或測試資料）。
        /// </summary>
        /// <param name="id">預約 Id。</param>
        /// <param name="reason">作廢原因，由彈出視窗填寫。</param>
        /// <param name="cancellationToken">使用者中斷請求（例如關閉頁面）時，取消進行中的資料庫操作。</param>
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
