using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VenueGo.Constants;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Models.ReservationModels;
using VenueGo.Options;
using VenueGo.Services.Orders;
using VenueGo.Services.Time;
using VenueGo.ViewModels.ReservationViewModels;

namespace VenueGo.Services.Reservations
{
    /// <summary>
    /// 建立預約服務的實作。
    /// </summary>
    public class ReservationCreationService : IReservationCreationService
    {
        /// <summary>訂單編號撞號時的重試次數。</summary>
        private const int MaxOrderNoAttempts = 3;

        private readonly dbVenueContext _db;
        private readonly ISlotSelectionValidator _slotValidator;
        private readonly IReservationPricingService _pricingService;
        private readonly IOrderNoGenerator _orderNoGenerator;
        private readonly ITimeService _timeService;
        private readonly ReservationRulesOptions _rules;
        private readonly ILogger<ReservationCreationService> _logger;

        public ReservationCreationService(
            dbVenueContext db,
            ISlotSelectionValidator slotValidator,
            IReservationPricingService pricingService,
            IOrderNoGenerator orderNoGenerator,
            ITimeService timeService,
            IOptionsSnapshot<ReservationRulesOptions> rules,
            ILogger<ReservationCreationService> logger)
        {
            _db = db;
            _slotValidator = slotValidator;
            _pricingService = pricingService;
            _orderNoGenerator = orderNoGenerator;
            _timeService = timeService;
            _rules = rules.Value;
            _logger = logger;
        }

        // ══ 後台代客建立 ═══════════════════════════════════

        public async Task<ReservationCreationResult> CreateAsync(
            ReservationDraft draft,
            ConfirmReservationInputModel input,
            int operatorUserId,
            CancellationToken cancellationToken = default)
        {
            // ── 驗證一：暫存資料是否完整 ──
            if (draft.MaxAllowedStep < 5)
            {
                return ReservationCreationResult.Fail("預約資料不完整，請重新完成前面的步驟。");
            }

            // ── 驗證二：確認頁的輸入 ──
            var venue = await LoadVenueAsync(draft.VenueId!.Value, cancellationToken);
            if (venue is null)
            {
                return ReservationCreationResult.Fail("場地不存在，請重新選擇。");
            }

            if (!venue.IsActive)
            {
                return ReservationCreationResult.Fail("此場地目前停用，無法建立預約。");
            }

            var inputErrors = ValidateInput(input, venue.Capacity);
            if (inputErrors.Count > 0)
            {
                return ReservationCreationResult.Fail(inputErrors);
            }

            // ── 驗證三：時段是否仍可預約 ──
            // 使用者停留在確認頁的期間，時段可能已被其他管理員訂走。
            // 這裡重新查一次資料庫，而不是相信暫存的資料。
            var validation = await _slotValidator.ValidateAsync(
                draft.VenueId.Value, draft.BookingDate!.Value, draft.SlotTimes, cancellationToken);

            if (!validation.IsValid)
            {
                return ReservationCreationResult.SlotConflict(
                    string.Join(" ", validation.Errors));
            }

            // ── 計價：一律由伺服器端重新計算 ──
            var pricing = _pricingService.Calculate(validation.Slots);

            if (!pricing.IsComplete)
            {
                return ReservationCreationResult.Fail(
                    "所選時段尚未設定價格，請先於場地管理設定該運動類型的計價規則。");
            }

            // ── 依後台的規則決定狀態、付款方式、期限，整理成寫入單 ──
            // 現在時間用網站時間（ITimeService），開發時才會跟著時光機；同一筆預約的時間欄位都用這一個值
            var now = _timeService.Now;
            var bookingDate = draft.BookingDate.Value;

            // 情境一（做法 B 開關）：開關開啟時，付款期限 = 建立時間 + 緩衝分鐘，
            // 代表員工代客收款時，最晚要在下單後這段時間內收到錢。
            // 開關關閉（組員預設情況）維持原本「使用時段開始時間」的算法，
            // BookingDate 是 date、StartTime 是 time，必須組成 datetime。
            var paymentDueAt = _rules.UseScenario1PaymentRule
                ? now.AddMinutes(_rules.PaymentCreationBufferMinutes)
                : bookingDate.ToDateTime(pricing.StartTime!.Value);

            // 已收款時預約直接進入已確認、訂單已付款；否則停在待確認、待付款
            var model = new ReservationWriteModel
            {
                UserId = draft.UserId!.Value,
                VenueId = draft.VenueId.Value,
                BookingDate = bookingDate,
                Pricing = pricing,

                PersonAmount = input.PersonAmount,
                InvoiceType = input.InvoiceType,
                // 選紙本發票時載具必須為 NULL，不可把使用者先前輸入的值留下
                CarrierNo = input.InvoiceType == InvoiceType.MobileBarcode
                    ? input.CarrierNo?.Trim()
                    : null,

                // 後台代客建立固定為現場付款 + 現場櫃台
                PaymentMethod = PaymentMethod.OnSite,
                PaymentChannel = PaymentChannel.Counter,
                PaymentDueAt = paymentDueAt,
                PaidAt = input.MarkAsPaid ? now : null,

                ReservationStatus = input.MarkAsPaid
                    ? ReservationStatus.Confirmed
                    : ReservationStatus.Pending,
                OrderStatus = input.MarkAsPaid ? OrderStatus.Paid : OrderStatus.AwaitingPayment,
                PaymentStatus = input.MarkAsPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid,

                // 稽核：UserId 是來打球的會員，OperatorUserId（CreatedBy）是操作系統的管理員
                Source = ReservationSource.Admin,
                OperatorUserId = operatorUserId,

                // 條款：後台代客建立時，代表管理員已向會員說明並確認同意
                TermsVersion = _rules.TermsVersion,
                Now = now
            };

            return await WriteAsync(model, cancellationToken);
        }

        // ══ 寫入（後台、前台共用）═══════════════════════════

        /// <summary>
        /// 寫入五張表，並在訂單編號撞號時重試。
        /// <para>
        /// 兩種唯一鍵衝突的處理方式完全不同：
        /// 訂單編號撞號可自動修復（換一個號再試，使用者不必知道）；
        /// 時段撞號無法修復（場地只有一個），必須讓使用者回去重新選擇時段。
        /// </para>
        /// </summary>
        public async Task<ReservationCreationResult> WriteAsync(
            ReservationWriteModel model,
            CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= MaxOrderNoAttempts; attempt++)
            {
                try
                {
                    return await WriteOnceAsync(model, cancellationToken);
                }
                catch (DbUpdateException ex) when (IsConflictOn(ex, "UQ_Orders_OrderNo"))
                {
                    // 訂單編號撞號：清掉追蹤中的實體，下一輪重新產生編號與實體。
                    // 不清的話，失敗的那批 Added 實體會留在 ChangeTracker 裡，
                    // 下一次 SaveChanges 會連同舊資料一起再送一遍。
                    _db.ChangeTracker.Clear();

                    if (attempt < MaxOrderNoAttempts) continue;

                    _logger.LogError(ex,
                        "訂單編號連續 {Attempts} 次產生失敗，可能不只是併發問題。",
                        MaxOrderNoAttempts);

                    return ReservationCreationResult.Fail("系統忙碌中，請稍後再試一次。");
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // 走到這裡表示撞的不是訂單編號，而是時段的佔位唯一鍵。
                    _db.ChangeTracker.Clear();

                    var message = await BuildSlotConflictMessageAsync(
                        model.VenueId, model.BookingDate, model.SlotTimes, cancellationToken);
                    return ReservationCreationResult.SlotConflict(message);
                }
            }

            return ReservationCreationResult.Fail("系統忙碌中，請稍後再試一次。");
        }

        /// <summary>
        /// 在單一交易內寫入五張表加一筆稽核紀錄。
        /// <para>
        /// 【為何分成多次 SaveChanges】子表需要主表產生的 Id
        /// （ReservationSlots 需要 ReservationId、OrdersDetails 需要 OrderId），
        /// 而資料庫未建立外鍵，EF 無法自動串接，只能先存再取 Id。
        /// 這裡刻意把佔位與訂單分成兩次儲存，
        /// 是為了讓唯一鍵衝突能明確歸屬於「時段」或「訂單編號」——
        /// 若合併成一次，兩種衝突會出現在同一個例外裡，難以分辨。
        /// 多幾次往返換來清楚的錯誤處理，在這個場景是值得的。
        /// </para>
        /// </summary>
        private async Task<ReservationCreationResult> WriteOnceAsync(
            ReservationWriteModel model,
            CancellationToken cancellationToken)
        {
            var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                // ── 1. 預約主檔 ──
                var reservation = BuildReservation(model);
                _db.Reservations.Add(reservation);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 2. 佔位。唯一鍵 UQ_ReservationSlots_Occupancy 在此擋下併發 ──
                AddReservationSlots(reservation, model);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 3. 訂單。唯一鍵 UQ_Orders_OrderNo 在此擋下撞號 ──
                var order = await BuildOrderAsync(reservation, model, cancellationToken);
                _db.Orders.Add(order);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 4. 明細、付款、稽核紀錄 ──
                AddOrderDetails(order, reservation, model.Pricing);
                AddPayment(order, model);
                AddAuditLog(reservation, order, model);
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return ReservationCreationResult.Success(reservation.ReservationId, order.OrderNo);
            }
        }

        // ── 各表的組裝：照寫入單填值，不做任何判斷 ─────────────

        /// <summary>組出預約主檔。</summary>
        private static Reservation BuildReservation(ReservationWriteModel model) => new()
        {
            UserId = model.UserId,
            VenueId = model.VenueId,
            BookingDate = model.BookingDate,

            // StartTime / EndTime 由計價結果的頭尾得出。
            // EndTime 是最後一格的起始時間加一小時：
            // 選了 17:00、18:00 兩格，結束時間是 19:00 而不是 18:00。
            StartTime = model.Pricing.StartTime!.Value,
            EndTime = model.Pricing.EndTime!.Value,

            ReservedAt = model.Now,
            ReservationStatus = (byte)model.ReservationStatus,

            TermsAcceptedAt = model.Now,
            TermsVersion = model.TermsVersion,

            CreatedBy = model.OperatorUserId,
            Source = (byte)model.Source
        };

        /// <summary>
        /// 組出佔位資料，一格時段一列。
        /// 這張表是時段是否被佔用的唯一依據，取消預約時必須刪除。
        /// </summary>
        private void AddReservationSlots(Reservation reservation, ReservationWriteModel model)
        {
            var slots = model.Pricing.Details.Select(line => new ReservationSlot
            {
                ReservationId = reservation.ReservationId,
                VenueId = model.VenueId,
                BookingDate = model.BookingDate,
                SlotTime = line.SlotTime
            });

            _db.ReservationSlots.AddRange(slots);
        }

        /// <summary>組出訂單。</summary>
        private async Task<Order> BuildOrderAsync(
            Reservation reservation,
            ReservationWriteModel model,
            CancellationToken cancellationToken)
        {
            return new Order
            {
                ReservationId = reservation.ReservationId,
                UserId = model.UserId,
                // 編號的日期跟下單時間用同一個值（model.Now 是網站時間，會跟著時光機）
                OrderNo = await _orderNoGenerator.GenerateAsync(model.Now, cancellationToken),
                InvoiceType = (byte)model.InvoiceType,
                CarrierNo = model.CarrierNo,
                OrderCreatedAt = model.Now,
                OrderStatus = (byte)model.OrderStatus,

                // 欄位目前仍是 PersonMount（錯字），改名後這一行改為 PersonAmount
                PersonMount = model.PersonAmount,

                // 總金額由計價結果提供，不在此另外相加，
                // 確保與 OrdersDetails 的小計總和必然一致
                TotalAmount = model.Pricing.TotalAmount
            };
        }

        /// <summary>
        /// 組出訂單明細，一格時段一列（方案 C）。
        /// UnitPrice、IsPeak 都是成交當下的快照，日後調價、改尖峰時間都不影響舊訂單。
        /// </summary>
        private void AddOrderDetails(Order order, Reservation reservation, PricingResult pricing)
        {
            var details = pricing.Details.Select(line => new OrdersDetail
            {
                OrderId = order.OrderId,
                ReservationId = reservation.ReservationId,
                SlotTime = line.SlotTime,
                UnitPrice = line.UnitPrice,
                DurationHours = line.DurationHours,
                Subtotal = line.Subtotal,
                // 計價時就決定了這格是尖峰還是離峰，跟單價是同一次判斷，存下來才不會跟單價對不上
                IsPeak = line.IsPeak
            });

            _db.OrdersDetails.AddRange(details);
        }

        /// <summary>
        /// 組出付款紀錄。
        /// <para>
        /// 這一列很容易被漏掉，但預約列表的付款狀態篩選就是從這裡來的——
        /// 沒有它，那筆預約的付款狀態會顯示空白。
        /// </para>
        /// </summary>
        private void AddPayment(Order order, ReservationWriteModel model)
        {
            _db.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                Amount = model.Pricing.TotalAmount,

                // 後台：現場付款 + 現場櫃台；前台：現場 + 櫃台，或線上 + 信用卡
                PaymentMethod = (byte)model.PaymentMethod,
                PaymentChannel = (byte)model.PaymentChannel,

                PaymentStatus = (byte)model.PaymentStatus,
                PaymentDueAt = model.PaymentDueAt,
                PaidAt = model.PaidAt,
                CreatedAt = model.Now
            });
        }

        /// <summary>新增一筆稽核紀錄，記錄是誰建立了這筆預約。</summary>
        private void AddAuditLog(Reservation reservation, Order order, ReservationWriteModel model)
        {
            var newValue = JsonSerializer.Serialize(new
            {
                reservation.ReservationStatus,
                order.OrderStatus,
                order.OrderNo,
                order.TotalAmount
            });

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = model.OperatorUserId,
                Action = AuditActions.CreateReservation,
                EntityType = AuditEntityTypes.Reservations,
                EntityId = reservation.ReservationId.ToString(),

                // 建立動作沒有「變更前」的狀態
                OldValue = null,
                NewValue = newValue,
                CreatedAt = model.Now
            });
        }

        // ── 驗證 ───────────────────────────────────────

        /// <summary>
        /// 驗證確認頁的輸入。
        /// 一次收集所有錯誤再回傳，讓使用者一次看到全部問題，
        /// 而不是改一個、送出、再被擋一次。
        /// </summary>
        private static List<string> ValidateInput(
            ConfirmReservationInputModel input, int? venueCapacity)
        {
            var errors = new List<string>();

            if (input.PersonAmount < 1)
            {
                errors.Add("使用人數至少 1 人。");
            }
            else if (venueCapacity.HasValue && input.PersonAmount > venueCapacity.Value)
            {
                errors.Add($"使用人數不可超過場地可容納人數 {venueCapacity.Value} 人。");
            }

            if (input.InvoiceType == InvoiceType.MobileBarcode)
            {
                var carrierNo = input.CarrierNo?.Trim();

                if (string.IsNullOrEmpty(carrierNo))
                {
                    errors.Add("選擇手機條碼載具時，載具號碼為必填。");
                }
                else if (!InvoiceRules.IsValidCarrierNo(carrierNo))   // 格式改用共用的 InvoiceRules
                {
                    errors.Add("載具號碼格式不正確，應為斜線加 7 碼大寫英數字，例如 /AB12345。");
                }
            }

            if (!input.TermsAccepted)
            {
                errors.Add("請先確認已向會員說明並同意租借條款。");
            }

            return errors;
        }

        // ── 共用的小工具 ────────────────────────────────

        private Task<Venue?> LoadVenueAsync(int venueId, CancellationToken ct)
            => _db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.VenueId == venueId, ct);

        /// <summary>
        /// 組出時段衝突的訊息，明確指出是哪幾格被搶走了。
        /// 只說「時段衝突」使用者不知道該改哪一格。
        /// </summary>
        private async Task<string> BuildSlotConflictMessageAsync(
            int venueId, DateOnly bookingDate, IReadOnlyList<TimeOnly> slotTimes,   // 不再依賴後台的草稿
            CancellationToken cancellationToken)
        {
            var validation = await _slotValidator.ValidateAsync(
                venueId, bookingDate, slotTimes, cancellationToken);

            if (validation.Errors.Count > 0)
            {
                return string.Join(" ", validation.Errors);
            }

            return "您選擇的時段剛被其他人預約，請重新選擇時段。";
        }

        /// <summary>
        /// 是否為唯一鍵衝突。
        /// 2627 是 UNIQUE 約束（ALTER TABLE ADD CONSTRAINT 建的）、
        /// 2601 是唯一索引（CREATE UNIQUE INDEX 建的）。
        /// 兩種本專案都有，必須都接。
        /// </summary>
        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.InnerException is SqlException sql
               && (sql.Number == 2601 || sql.Number == 2627);

        /// <summary>
        /// 是否為指定索引造成的衝突。
        /// <para>
        /// SQL Server 的錯誤訊息裡會帶索引名稱，例如
        /// Violation of UNIQUE KEY constraint 'UQ_Orders_OrderNo'.
        /// 靠解析字串判斷並不優雅，但 SqlException 沒有提供
        /// 「違反的是哪個索引」的獨立屬性，實務上只能這樣做。
        /// </para>
        /// </summary>
        private static bool IsConflictOn(DbUpdateException ex, string indexName)
            => IsUniqueViolation(ex)
               && ex.InnerException!.Message.Contains(
                   indexName, StringComparison.OrdinalIgnoreCase);
    }
}
