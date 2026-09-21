using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VenueGo.Data;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.Models.Options;
using VenueGo.Models.ReservationModels;
using VenueGo.Services.Orders;
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

        /// <summary>手機條碼載具的格式：斜線加 7 碼大寫英數字或 + - . 三個符號。</summary>
        private static readonly Regex CarrierNoPattern =
            new(@"^/[0-9A-Z+\-.]{7}$", RegexOptions.Compiled);

        private readonly dbVenueContext _db;
        private readonly ISlotSelectionValidator _slotValidator;
        private readonly IReservationPricingService _pricingService;
        private readonly IOrderNoGenerator _orderNoGenerator;
        private readonly ReservationRulesOptions _rules;
        private readonly ILogger<ReservationCreationService> _logger;

        public ReservationCreationService(
            dbVenueContext db,
            ISlotSelectionValidator slotValidator,
            IReservationPricingService pricingService,
            IOrderNoGenerator orderNoGenerator,
            IOptionsSnapshot<ReservationRulesOptions> rules,
            ILogger<ReservationCreationService> logger)
        {
            _db = db;
            _slotValidator = slotValidator;
            _pricingService = pricingService;
            _orderNoGenerator = orderNoGenerator;
            _rules = rules.Value;
            _logger = logger;
        }

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

            return await WriteWithRetryAsync(
                draft, input, pricing, operatorUserId, cancellationToken);
        }

        // ══ 寫入 ═══════════════════════════════════════

        /// <summary>
        /// 寫入五張表，並在訂單編號撞號時重試。
        /// <para>
        /// 兩種唯一鍵衝突的處理方式完全不同：
        /// 訂單編號撞號可自動修復（換一個號再試，使用者不必知道）；
        /// 時段撞號無法修復（場地只有一個），必須讓使用者回步驟 4 重新選擇。
        /// </para>
        /// </summary>
        private async Task<ReservationCreationResult> WriteWithRetryAsync(
            ReservationDraft draft,
            ConfirmReservationInputModel input,
            PricingResult pricing,
            int operatorUserId,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= MaxOrderNoAttempts; attempt++)
            {
                try
                {
                    return await WriteOnceAsync(
                        draft, input, pricing, operatorUserId, cancellationToken);
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

                    var message = await BuildSlotConflictMessageAsync(draft, cancellationToken);
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
            ReservationDraft draft,
            ConfirmReservationInputModel input,
            PricingResult pricing,
            int operatorUserId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var bookingDate = draft.BookingDate!.Value;
            var startTime = pricing.StartTime!.Value;

            // 付款期限 = 使用時段的開始時間，代表會員最晚要在報到前付清。
            // BookingDate 是 date、StartTime 是 time，必須組成 datetime。
            var paymentDueAt = bookingDate.ToDateTime(startTime);

            var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await using (transaction)
            {
                // ── 1. 預約主檔 ──
                var reservation = BuildReservation(draft, pricing, input, operatorUserId, now);
                _db.Reservations.Add(reservation);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 2. 佔位。唯一鍵 UQ_ReservationSlots_Occupancy 在此擋下併發 ──
                AddReservationSlots(reservation, draft, pricing);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 3. 訂單。唯一鍵 UQ_Orders_OrderNo 在此擋下撞號 ──
                var order = await BuildOrderAsync(
                    reservation, draft, input, pricing, now, cancellationToken);
                _db.Orders.Add(order);
                await _db.SaveChangesAsync(cancellationToken);

                // ── 4. 明細、付款、稽核紀錄 ──
                AddOrderDetails(order, reservation, pricing);
                AddPayment(order, input, pricing, paymentDueAt, now);
                AddAuditLog(reservation, order, operatorUserId, now);
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return ReservationCreationResult.Success(reservation.ReservationId, order.OrderNo);
            }
        }

        // ── 各表的組裝 ──────────────────────────────────

        /// <summary>組出預約主檔。</summary>
        private Reservation BuildReservation(
            ReservationDraft draft,
            PricingResult pricing,
            ConfirmReservationInputModel input,
            int operatorUserId,
            DateTime now)
        {
            // 已收款時預約直接進入已確認；否則停在待確認等候付款。
            var reservationStatus = input.MarkAsPaid
                ? ReservationStatus.Confirmed
                : ReservationStatus.Pending;

            return new Reservation
            {
                UserId = draft.UserId!.Value,
                VenueId = draft.VenueId!.Value,
                BookingDate = draft.BookingDate!.Value,

                // StartTime / EndTime 由計價結果的頭尾得出。
                // EndTime 是最後一格的起始時間加一小時：
                // 選了 17:00、18:00 兩格，結束時間是 19:00 而不是 18:00。
                StartTime = pricing.StartTime!.Value,
                EndTime = pricing.EndTime!.Value,

                ReservedAt = now,
                ReservationStatus = (byte)reservationStatus,

                // 條款：後台代客建立時，代表管理員已向會員說明並確認同意
                TermsAcceptedAt = now,
                TermsVersion = _rules.TermsVersion,

                // 稽核：UserId 是來打球的會員，CreatedBy 是操作系統的管理員
                CreatedBy = operatorUserId,
                Source = (byte)ReservationSource.Admin
            };
        }

        /// <summary>
        /// 組出佔位資料，一格時段一列。
        /// 這張表是時段是否被佔用的唯一依據，取消預約時必須刪除。
        /// </summary>
        private void AddReservationSlots(
            Reservation reservation, ReservationDraft draft, PricingResult pricing)
        {
            var slots = pricing.Details.Select(line => new ReservationSlot
            {
                ReservationId = reservation.ReservationId,
                VenueId = draft.VenueId!.Value,
                BookingDate = draft.BookingDate!.Value,
                SlotTime = line.SlotTime
            });

            _db.ReservationSlots.AddRange(slots);
        }

        /// <summary>組出訂單。</summary>
        private async Task<Order> BuildOrderAsync(
            Reservation reservation,
            ReservationDraft draft,
            ConfirmReservationInputModel input,
            PricingResult pricing,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var orderStatus = input.MarkAsPaid ? OrderStatus.Paid : OrderStatus.AwaitingPayment;

            // 選紙本發票時載具必須為 NULL，不可把使用者先前輸入的值留下
            var carrierNo = input.InvoiceType == InvoiceType.MobileBarcode
                ? input.CarrierNo?.Trim()
                : null;

            return new Order
            {
                ReservationId = reservation.ReservationId,
                UserId = draft.UserId!.Value,
                OrderNo = await _orderNoGenerator.GenerateAsync(cancellationToken),
                InvoiceType = (byte)input.InvoiceType,
                CarrierNo = carrierNo,
                OrderCreatedAt = now,
                OrderStatus = (byte)orderStatus,

                // 欄位目前仍是 PersonMount（錯字），改名後這一行改為 PersonAmount
                PersonMount = input.PersonAmount,

                // 總金額由計價結果提供，不在此另外相加，
                // 確保與 OrdersDetails 的小計總和必然一致
                TotalAmount = pricing.TotalAmount
            };
        }

        /// <summary>
        /// 組出訂單明細，一格時段一列（方案 C）。
        /// UnitPrice 是成交當下的價格快照，日後調價不影響舊訂單。
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
                Subtotal = line.Subtotal
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
        private void AddPayment(
            Order order,
            ConfirmReservationInputModel input,
            PricingResult pricing,
            DateTime paymentDueAt,
            DateTime now)
        {
            var paymentStatus = input.MarkAsPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid;

            _db.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                Amount = pricing.TotalAmount,

                // 後台代客建立固定為現場付款 + 現場櫃台。
                // 期末線上付款會是 Online + CreditCard / LinePay。
                PaymentMethod = (byte)PaymentMethod.OnSite,
                PaymentChannel = (byte)PaymentChannel.Counter,

                PaymentStatus = (byte)paymentStatus,
                PaymentDueAt = paymentDueAt,
                PaidAt = input.MarkAsPaid ? now : null,
                CreatedAt = now
            });
        }

        /// <summary>新增一筆稽核紀錄，記錄是誰建立了這筆預約。</summary>
        private void AddAuditLog(
            Reservation reservation, Order order, int operatorUserId, DateTime now)
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
                UserId = operatorUserId,
                Action = AuditActions.CreateReservation,
                EntityType = AuditEntityTypes.Reservations,
                EntityId = reservation.ReservationId.ToString(),

                // 建立動作沒有「變更前」的狀態
                OldValue = null,
                NewValue = newValue,
                CreatedAt = now
            });
        }

        // ── 驗證 ───────────────────────────────────────

        /// <summary>
        /// 驗證確認頁的輸入。
        /// 一次收集所有錯誤再回傳，讓使用者一次看到全部問題，
        /// 而不是改一個、送出、再被擋一次。
        /// </summary>
        private List<string> ValidateInput(
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
                else if (!CarrierNoPattern.IsMatch(carrierNo))
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
            ReservationDraft draft, CancellationToken cancellationToken)
        {
            var validation = await _slotValidator.ValidateAsync(
                draft.VenueId!.Value, draft.BookingDate!.Value, draft.SlotTimes, cancellationToken);

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