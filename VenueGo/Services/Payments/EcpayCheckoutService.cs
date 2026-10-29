using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Options;
using VenueGo.Models.PaymentModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Payments
{
    /// <summary>
    /// 開始付款的實作。
    /// <para>
    /// 【防止重複開單】付款改成處理中用 ExecuteUpdateAsync 加上「還是未付款或付款失敗」的條件
    /// （同 ReservationExpiryService、PaymentConfirmationService）。兩個請求同時進來，只有一個更新得到，
    /// 另一個更新到 0 筆就回 Conflict，所以不會產生兩張付款單。不能只靠前端把按鈕停用
    /// </para>
    /// <para>
    /// 【兩種時間】交易編號、MerchantTradeDate 用 TimeProvider 的真實時間（綠界是外部系統，不跟著時光機）；
    /// 能不能付款、資料庫的 CreatedAt 用 ITimeService，跟 3-B 的付款期限同一套規則
    /// </para>
    /// <para>
    /// 【付款處理中又按付款】（3-C-5 決定 2：B）先請付款確認員問綠界一次，再照新的狀態重新檢查：
    /// 已付款就回「已經付款」、失敗或太久沒付就馬上開新的付款單、還不知道結果就擋下並告訴客人要等多久。
    /// 同一筆隔 RequeryInterval 才再問，客人連按也不會讓綠界擋我們
    /// </para>
    /// </summary>
    public class EcpayCheckoutService : IEcpayCheckoutService
    {
        /// <summary>交易編號撞號時最多試幾次</summary>
        private const int MaxTradeNoAttempts = 3;

        // RawData 裡的中文保持原樣，不要變成 \u5834，在 SSMS 查看比較好讀
        private static readonly JsonSerializerOptions RawDataJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        private readonly dbVenueContext _db;
        private readonly ITimeService _timeService;
        private readonly TimeProvider _timeProvider;
        private readonly EcpayOptions _ecpay;
        private readonly ReservationRulesOptions _rules;
        private readonly IEcpayPaymentVerifier _verifier;
        private readonly ILogger<EcpayCheckoutService> _logger;

        public EcpayCheckoutService(
            dbVenueContext db,
            ITimeService timeService,
            TimeProvider timeProvider,
            IOptionsSnapshot<EcpayOptions> ecpay,
            IOptionsSnapshot<ReservationRulesOptions> rules,
            IEcpayPaymentVerifier verifier,
            ILogger<EcpayCheckoutService> logger)
        {
            _db = db;
            _timeService = timeService;

            // Program.cs 註冊的是 TaipeiTimeProvider：真實時間，時區固定台北
            _timeProvider = timeProvider;

            // IOptionsSnapshot：每個請求重新讀一次設定（同 MemberOrderQueryService）
            _ecpay = ecpay.Value;
            _rules = rules.Value;

            _verifier = verifier;
            _logger = logger;
        }

        public async Task<StartPaymentResult> StartAsync(
            int userId, int orderId, CancellationToken cancellationToken = default)
        {
            var target = await FindTargetAsync(userId, orderId, cancellationToken);

            // 付款處理中：先問綠界一次，問完狀態可能變了（已付款、失敗、改回未付款），所以重新讀一次
            if (IsProcessing(target))
            {
                await AskEcpayIfAllowedAsync(target!, cancellationToken);
                target = await FindTargetAsync(userId, orderId, cancellationToken);
            }

            // 還是處理中：不知道上一筆有沒有扣款，不能開新的付款單，告訴客人大概要等多久
            if (IsProcessing(target))
            {
                return await InProgressAsync(target!, cancellationToken);
            }

            var rejected = Check(target, _timeService.Now);
            if (rejected is not null) return rejected;

            return await CreateCheckoutAsync(target!, cancellationToken);
        }

        private static bool IsProcessing(Target? t)
            => t?.PaymentStatus == (byte)PaymentStatus.Processing;

        /// <summary>處理需要的資料（只讀，不追蹤）</summary>
        private sealed record Target(
            int OrderId, string OrderNo, byte OrderStatus,
            byte ReservationStatus,
            int PaymentId, int Amount, byte PaymentMethod, byte PaymentStatus, DateTime PaymentDueAt);

        /// <summary>
        /// 讀訂單＋預約＋最新一筆付款。條件裡有 UserId：別人的訂單跟不存在一樣查不到
        /// （「最新一筆」的規則同訂單列表、逾期排程）
        /// </summary>
        private Task<Target?> FindTargetAsync(int userId, int orderId, CancellationToken ct)
        {
            return (from o in _db.Orders.AsNoTracking()
                    // Orders.ReservationId 是 int?，要轉型才能跟 int 比對
                    join r in _db.Reservations.AsNoTracking() on o.ReservationId equals (int?)r.ReservationId
                    let p = _db.Payments
                        .Where(x => x.OrderId == o.OrderId)
                        .OrderByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.PaymentId)
                        .FirstOrDefault()
                    where o.OrderId == orderId
                       && o.UserId == userId
                       && p != null
                    select new Target(
                        o.OrderId, o.OrderNo, o.OrderStatus,
                        r.ReservationStatus,
                        p.PaymentId, p.Amount, p.PaymentMethod, p.PaymentStatus, p.PaymentDueAt))
                   .FirstOrDefaultAsync(ct);
        }

        /// <summary>檢查能不能開始付款。可以就回傳 null；不行就回傳原因（這時還沒改任何資料）</summary>
        private StartPaymentResult? Check(Target? t, DateTime now)
        {
            if (t is null)
            {
                return Fail(StartPaymentOutcome.NotFound, "找不到這筆訂單。");
            }

            if (t.PaymentMethod != (byte)PaymentMethod.Online)
            {
                return Fail(StartPaymentOutcome.NotOnlinePayment, "這筆訂單是現場付款，請在付款期限前到櫃台付款。");
            }

            if (t.PaymentStatus is (byte)PaymentStatus.Paid
                or (byte)PaymentStatus.Refunding
                or (byte)PaymentStatus.Refunded)
            {
                return Fail(StartPaymentOutcome.AlreadyPaid, "這筆訂單已經付款。");
            }

            // 付款處理中不會走到這裡：StartAsync 已經先問過綠界，還是處理中就由 InProgressAsync 擋下

            if (t.ReservationStatus != (byte)ReservationStatus.Pending
                || t.OrderStatus != (byte)OrderStatus.AwaitingPayment
                || t.PaymentStatus is not ((byte)PaymentStatus.Unpaid or (byte)PaymentStatus.Failed))
            {
                return Fail(StartPaymentOutcome.NotPayable, "這筆訂單目前無法付款（可能已取消或已逾期）。");
            }

            // 最晚開始付款時間用 3-B 的同一個方法，跟訂單列表的倒數、付款按鈕一致
            if (now >= _rules.GetMemberOnlinePaymentStartDeadline(t.PaymentDueAt))
            {
                return Fail(StartPaymentOutcome.PastStartDeadline,
                    $"已超過可以開始付款的時間（付款期限前 {_rules.MemberOnlinePaymentStartCutoffMinutes} 分鐘）。");
            }

            // 綠界的 TotalAmount 只收正整數新台幣
            if (t.Amount <= 0)
            {
                _logger.LogWarning("訂單 {OrderNo} 金額異常：{Amount}，無法開始付款", t.OrderNo, t.Amount);
                return Fail(StartPaymentOutcome.InvalidAmount, "訂單金額異常，請聯絡櫃台。");
            }

            if (!_ecpay.IsConfigured)
            {
                return Fail(StartPaymentOutcome.NotConfigured, "線上付款暫時無法使用，請稍後再試或改到櫃台付款。");
            }

            return null;
        }

        // ══ 付款處理中又按付款（3-C-5）═══════════════════════════════

        /// <summary>上一張付款單：哪一張、什麼時候送出、最後一次問綠界的時間（沒問過是 null）</summary>
        private sealed record PendingSend(string MerchantTradeNo, DateTime SentAt, DateTime? LastQueriedAt);

        private EcpayReconciliationOptions Reconciliation => _ecpay.Reconciliation;

        /// <summary>
        /// 最近沒問過的話，請付款確認員問綠界一次（處理規則跟排程對帳、收到通知一樣）。
        /// 不用等「送出滿 10 分鐘」：客人回到網站按付款，代表已經離開綠界的頁面了。
        /// 但「同一筆隔 RequeryInterval 才再問」要遵守：客人連按也只會問一次
        /// </summary>
        private async Task AskEcpayIfAllowedAsync(Target t, CancellationToken ct)
        {
            var send = await FindPendingSendAsync(t.PaymentId, ct);
            if (send is null || !CanQueryAgain(send, _timeService.Now)) return;

            var decision = await _verifier.VerifyAsync(
                new EcpayPaymentTarget(t.PaymentId, t.OrderNo), send.MerchantTradeNo, ct);

            _logger.LogInformation(
                "訂單 {OrderNo} 付款處理中又按付款，先問綠界（{MerchantTradeNo}）→ {Outcome}",
                t.OrderNo, send.MerchantTradeNo, decision.Outcome);
        }

        /// <summary>
        /// 擋下並告訴客人大概要等多久：要等到「送出滿 AbandonAfter」而且「可以再問綠界」，
        /// 排程（或客人再按一次）才會把付款改回未付款
        /// </summary>
        private async Task<StartPaymentResult> InProgressAsync(Target t, CancellationToken ct)
        {
            var now = _timeService.Now;
            var send = await FindPendingSendAsync(t.PaymentId, ct);
            var retryAt = send is null ? (DateTime?)null : EstimateRetryAt(send);

            // 估不出來，或等到那時已經過了最晚開始付款時間：不給分鐘數，免得客人白等
            if (retryAt is null || retryAt >= _rules.GetMemberOnlinePaymentStartDeadline(t.PaymentDueAt))
            {
                return Fail(StartPaymentOutcome.InProgress, "上一筆付款仍在確認中，結果確認後會顯示在訂單頁。");
            }

            var minutes = Math.Max(1, (int)Math.Ceiling((retryAt.Value - now).TotalMinutes));
            return Fail(StartPaymentOutcome.InProgress,
                $"上一筆付款仍在確認中。如果你已經關閉綠界的付款頁、沒有付款，大約 {minutes} 分鐘後可以重新付款。");
        }

        /// <summary>
        /// 什麼時候可以重新付款：送出滿 AbandonAfter，而且距離上次問綠界滿 RequeryInterval，取比較晚的那個
        /// </summary>
        private DateTime EstimateRetryAt(PendingSend send)
        {
            var abandonedAt = send.SentAt + Reconciliation.AbandonAfter;
            var canQueryAt = send.LastQueriedAt + Reconciliation.RequeryInterval;

            return canQueryAt > abandonedAt ? canQueryAt.Value : abandonedAt;
        }

        /// <summary>同一筆隔 RequeryInterval 才再問（排程對帳 PaymentReconciliationService 也是同一個規則）</summary>
        private bool CanQueryAgain(PendingSend send, DateTime now)
            => send.LastQueriedAt is null || now - send.LastQueriedAt.Value >= Reconciliation.RequeryInterval;

        /// <summary>最新那張付款單（處理中的就是它），以及這筆付款最後一次問綠界的時間</summary>
        private Task<PendingSend?> FindPendingSendAsync(int paymentId, CancellationToken ct)
        {
            return _db.PaymentTransactions.AsNoTracking()
                .Where(t => t.PaymentId == paymentId
                         && t.EventType == (byte)PaymentTransactionEventType.Send)
                .OrderByDescending(t => t.PaymentTransactionId)
                .Select(t => new PendingSend(
                    t.MerchantTradeNo,
                    t.CreatedAt,
                    _db.PaymentTransactions
                        .Where(q => q.PaymentId == paymentId
                                 && q.EventType == (byte)PaymentTransactionEventType.QueryTradeInfo)
                        .Max(q => (DateTime?)q.CreatedAt)))
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>付款改成處理中＋記錄送出付款＋產生付款單，在同一個交易裡</summary>
        private async Task<StartPaymentResult> CreateCheckoutAsync(Target t, CancellationToken ct)
        {
            // 沒有 CommitAsync 就離開這個方法時，交易會自動 Rollback，付款狀態也會改回去
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            // 1. 搶位子：還是未付款或付款失敗，才改成處理中。
            //    同時送兩次的話，第二次更新到 0 筆，就不會再開第二張付款單
            var locked = await _db.Payments
                .Where(p => p.PaymentId == t.PaymentId
                         && (p.PaymentStatus == (byte)PaymentStatus.Unpaid
                             || p.PaymentStatus == (byte)PaymentStatus.Failed))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PaymentStatus, (byte)PaymentStatus.Processing), ct);
            if (locked == 0)
            {
                return Fail(StartPaymentOutcome.Conflict, "付款狀態剛被變更，請重新整理頁面後再試。");
            }

            var now = _timeService.Now;

            // 2. 產生付款單並記錄。交易編號撞到唯一索引 UQ_PaymentTransactions_MerchantTradeNo_Send 就換一組
            for (var attempt = 1; attempt <= MaxTradeNoAttempts; attempt++)
            {
                var realNow = _timeProvider.GetLocalNow();
                var tradeNo = MerchantTradeNoGenerator.Create(realNow);
                var fields = BuildFields(t, tradeNo, realNow);

                var record = new PaymentTransaction
                {
                    PaymentId = t.PaymentId,
                    MerchantTradeNo = tradeNo,
                    EventType = (byte)PaymentTransactionEventType.Send,
                    TradeAmount = t.Amount,
                    ProcessResult = (byte)PaymentTransactionProcessResult.LogOnly,

                    // 送出去的內容（沒有 HashKey、HashIV），之後跟綠界對帳、查問題用
                    RawData = JsonSerializer.Serialize(fields, RawDataJsonOptions),
                    CreatedAt = now
                };
                _db.PaymentTransactions.Add(record);

                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // 撞號：把這筆從 DbContext 拿掉，換一組再試
                    // （交易裡已經有一個 SaveChanges 失敗，EF Core 會自動退回到這次 SaveChanges 之前，前面的更新還在）
                    _db.Entry(record).State = EntityState.Detached;
                    _logger.LogWarning("交易編號 {MerchantTradeNo} 已經用過，換一組重試（第 {Attempt} 次）", tradeNo, attempt);
                    continue;
                }

                await transaction.CommitAsync(ct);

                // log 只記識別資料，不記 HashKey、HashIV、CheckMacValue
                _logger.LogInformation(
                    "訂單 {OrderNo} 開始線上付款：付款 {PaymentId}，交易編號 {MerchantTradeNo}，金額 {Amount}",
                    t.OrderNo, t.PaymentId, tradeNo, t.Amount);

                return new StartPaymentResult(
                    StartPaymentOutcome.Started, "已建立付款單。", _ecpay.CheckoutUrl, fields);
            }

            _logger.LogError("訂單 {OrderNo} 交易編號連續 {Count} 次撞號，放棄開始付款", t.OrderNo, MaxTradeNoAttempts);
            return Fail(StartPaymentOutcome.TradeNoUnavailable, "暫時無法建立付款，請稍後再試。");
        }

        /// <summary>
        /// 綠界 AioCheckOut V5 的欄位（官方文件「產生訂單」2862）。
        /// 值全部是字串：送出時是表單欄位，CheckMacValue 也是用字串算的
        /// </summary>
        private Dictionary<string, string> BuildFields(Target t, string tradeNo, DateTimeOffset realNow)
        {
            var fields = new Dictionary<string, string>
            {
                ["MerchantID"] = _ecpay.MerchantId,
                ["MerchantTradeNo"] = tradeNo,

                ["MerchantTradeDate"] = EcpayProtocol.FormatDateTime(realNow),
                ["PaymentType"] = "aio",

                // 金額只從資料庫來，前端沒辦法指定
                ["TotalAmount"] = t.Amount.ToString(CultureInfo.InvariantCulture),

                // TradeDesc 不能有特殊符號；ItemName 的 # 是「分隔多個商品」，所以不能出現 #
                ["TradeDesc"] = "VenueGo場地預約",
                ["ItemName"] = $"場地預約 {t.OrderNo}",

                // 網址都從後端設定來，不接受前端傳入。
                // ReturnURL 是綠界的伺服器連過來，要對外連得到（ngrok）；OrderResultURL 是客人的瀏覽器連過來，localhost 就可以
                ["ReturnURL"] = CombineUrl(_ecpay.GetNotifyBaseUrl(), EcpayProtocol.Routes.Return),
                ["OrderResultURL"] = CombineUrl(_ecpay.BackendBaseUrl, EcpayProtocol.Routes.Result),
                // 「返回商店」按鈕。有 OrderResultURL 時綠界會優先用它；OrderResultURL 不支援的付款方式（例如銀聯卡）
                // 才會顯示這個按鈕（只是一個連結，不會帶付款結果回來）
                ["ClientBackURL"] = CombineUrl(_ecpay.FrontendBaseUrl, FrontendRoutes.MemberOrders),

                ["ChoosePayment"] = "Credit",
                ["EncryptType"] = "1",
            };

            fields[EcpayCheckMac.FieldName] = EcpayCheckMac.Create(fields, _ecpay.HashKey, _ecpay.HashIV);
            return fields;
        }

        /// <summary>設定檔的網址結尾多打一個 / 也不會變成 //</summary>
        private static string CombineUrl(string baseUrl, string path)
            => baseUrl.TrimEnd('/') + path;

        private static StartPaymentResult Fail(StartPaymentOutcome outcome, string message)
            => new(outcome, message);

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is SqlException sql
               && (sql.Number == 2601 || sql.Number == 2627);
    }
}