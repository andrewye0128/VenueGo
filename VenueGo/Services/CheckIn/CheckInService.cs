using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Models.Entities;
using VenueGo.Services.ReviewTickets;
using VenueGo.Services.Time;
using static VenueGo.Services.CheckIn.ICheckInService;

namespace VenueGo.Services.CheckIn
{
    public class CheckInService : ICheckInService
    {
        private readonly dbVenueContext _db;
        private readonly IVisitReviewTicketFactory _visitReviewTicketFactory;
        private readonly ILogger<CheckInService> _logger;
        private readonly ITimeService _time;

        public CheckInService(dbVenueContext db, IVisitReviewTicketFactory visitReviewTicketFactory, ITimeService time, ILogger<CheckInService> logger)
        {

            _db = db;
            _time = time;
            _logger = logger;
            _visitReviewTicketFactory = visitReviewTicketFactory;
        }


        // 儀錶板 -> 只撈今天以前的票
        public async Task<int> SettleAllDueTicketsAsync()
        {
            var today = DateOnly.FromDateTime(_time.Now);
            // 只有 Valid/Used 是非終態,才需要檢查是否過期
            var candidateTickets = await (from t in _db.EntryTickets
                                          join o in _db.Orders on t.OrderId equals o.OrderId
                                          join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                          where (t.Status == (byte)EntryTicketStatus.Valid ||
                                                 t.Status == (byte)EntryTicketStatus.Used)
                                                && r.BookingDate <= today
                                          select t).ToListAsync();

            int settledCount = 0;
            foreach (var ticket in candidateTickets)
            {
                // 逐張複用既有判斷邏輯,結算條件/DateOnly+TimeOnly比較都在這支裡面做,不用在SQL端組日期時間
                if (await SettleIfPastEndAsync(ticket))
                {
                    settledCount++;
                }
            }

            return settledCount;
        }

        public async Task<int> SettleOvertimeAsync()
        {
            var now = _time.Now;
            var today = DateOnly.FromDateTime(now);
            var earliest = today.AddDays(-1);   // 只看昨天和今天,舊的不再追

            var candidates = await (from t in _db.EntryTickets
                                    join o in _db.Orders on t.OrderId equals o.OrderId
                                    join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                    where t.Status == (byte)EntryTicketStatus.Expired
                                          && r.BookingDate >= earliest && r.BookingDate <= today
                                          && !_db.OvertimeCharges.Any(c => c.OrderId == t.OrderId)
                                    select new { t.TicketId, t.OrderId, r.BookingDate, r.EndTime })
                                   .ToListAsync();

            var handledOrders = new HashSet<int>();   // 同訂單多張票只建一筆
            int count = 0;

            foreach (var c in candidates)
            {
                if (handledOrders.Contains(c.OrderId)) continue;

                var endAt = c.BookingDate.ToDateTime(c.EndTime);
                if (now < endAt.AddMinutes(31)) continue;   // 金額還沒封頂,先不建

                var lastLog = await GetLastInOutLogAsync(c.TicketId);
                bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;
                if (!isInside) continue;

                var fee = OvertimeFee.Calculate(endAt, now);
                if (fee.IsFree) continue;

                _db.OvertimeCharges.Add(new OvertimeCharge
                {
                    OrderId = c.OrderId,
                    TicketId = c.TicketId,
                    OverdueMinutes = fee.OverdueMinutes,
                    UnitPrice = fee.UnitPrice,
                    BillingHours = fee.BillingHours,
                    ChargeAmount = fee.ChargeAmount,
                    Status = (byte)OvertimeChargeStatus.Pending,
                    Remark = "排程代建：未掃碼離場",
                    OperatorId = null,
                    CreatedAt = now,
                    ConfirmedAt = null
                });


                // 衝突處理 ex:員工手動/客人出場建立待付款超時紀錄和排程同時建立 -> log顯示
                try
                {
                    await _db.SaveChangesAsync();
                    handledOrders.Add(c.OrderId);
                    count++;
                }
                catch(DbUpdateException e)
                {
                    _db.ChangeTracker.Clear();   // 把失敗那筆移出追蹤
                    handledOrders.Add(c.OrderId); // 同訂單也要一起紀錄移除不處理
                    _logger.LogWarning(e, "排程代建超時代收失敗。OrderId={OrderId}", c.OrderId);
                }
            }
            return count;
        }

        // 員工快速處理
        public async Task<OvertimePaymentResult> QuickOvertimeChargeAsync(int ticketId)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return OvertimePaymentResult.Fail("找不到這張票券");
            }

            // 還是 Used 但已過預約結束時間的票,先當場轉 Expired
            await SettleIfPastEndAsync(ticket);

            var lastLog = await GetLastInOutLogAsync(ticketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;
            if (ticket.Status != (byte)EntryTicketStatus.Expired || !isInside)
            {
                return OvertimePaymentResult.Fail("這張票券目前不需要結帳（可能已處理過）");
            }

            OvertimeFeeResult? fee = await CalculateOvertimeFeeAsync(ticket);
            if (fee == null)
            {
                return OvertimePaymentResult.Fail("找不到這張票券的預約資料");
            }
            if (fee.IsFree)
            {
                return OvertimePaymentResult.Fail("目前在寬限時間內免收費，請使用一般手動離場");
            }

            try
            {
                // 回傳 false = 同訂單已收/已免收,不需要再結帳
                bool needPaid = await EnsurePendingChargeAsync(ticket, fee);
                await _db.SaveChangesAsync();   // Ensure 只 Add 不 Save,這裡才真的寫入
                if (!needPaid)
                {
                    return OvertimePaymentResult.Fail("這筆訂單的超時費用已處理，請使用一般手動離場");
                }
            }
            catch (DbUpdateException ex)
            {
                // 剛好跟排程或出場同時建了同訂單的紀錄(OrderId 唯一索引)
                _db.ChangeTracker.Clear();
                _logger.LogWarning(ex, "建立待收衝突。OrderId={OrderId}", ticket.OrderId);
                return OvertimePaymentResult.Fail("這筆訂單剛剛已有超時紀錄，請重新整理");
            }

            return OvertimePaymentResult.OK(fee.ChargeAmount);
        }

        public async Task SettleTicketAsync(int ticketId)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket != null) await SettleIfPastEndAsync(ticket);
        }

        public async Task<CheckInResult> CheckInAsync(int ticketId, int? operatorId, bool isManualOverride)
        {
            // 找無票券
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            // 處理票券票券超過時間狀態
            await SettleIfPastEndAsync(ticket);

            // --------------------------- 驗證票券 ---------------------------
            // 第一層驗證 - 票證為不合法序號
            // 票證取消(已申請退款流程)
            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyCancelled);
            }

            // 票證逾期(已超過預約時間)
            if (ticket.Status == (byte)EntryTicketStatus.Expired)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyExpired);
            }

            // 票證已完成(預約訂單已結束)
            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.AlreadyCompleted);
            }

            // 第二層驗證 - 票證合法但不符合入場條件
            // 未到預約時間
            var reservation = await GetReservationTime(ticket.OrderId);
            if (reservation != null && _time.Now < reservation.Value.BookingDate.ToDateTime(reservation.Value.StartTIme))
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckIn, operatorId, isManualOverride, CheckInFailReason.NotYetStartTime);
            }

            // 第三層驗證 - 最後一層判斷 - 票證出入是否異常 - 查詢最後出入場的紀錄 - 無紀錄票證轉已使用 - 成功寫入出入場紀錄
            var lastlog = await GetLastInOutLogAsync(ticketId);
            //無紀錄或以出場達成入場條件
            bool isVaild = lastlog == null || lastlog.Action == (byte)CheckInAction.CheckOut;
            bool isFirstEntry = isVaild && ticket.Status == (byte)EntryTicketStatus.Valid;
            if (isFirstEntry)
            {
                ticket.Status = (byte)EntryTicketStatus.Used;
            }

            // 新增出入廠動作和紀錄
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.CheckIn,
                ActionTime = _time.Now,
                IsValid = isVaild,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId,
                FailReason = isVaild ? null : (byte)CheckInFailReason.InvalidSequence
            });

            await _db.SaveChangesAsync();

            // 當入場成功時呼叫, 這裡多一個判斷首次入場條件, 優化CreateReviewPerVisitAsync(有防呆)多次不必要查詢
            if (isFirstEntry)
            {
                await _visitReviewTicketFactory.CreateReviewPerVisitAsync(ticket.Qrtoken);
            }
            // 回傳驗證結果(前台顯示進出成功或是失敗)
            return isVaild ? CheckInResult.Ok() : CheckInResult.Fail(CheckInFailReason.InvalidSequence);
        }

        public async Task<CheckInResult> CheckOutAsync(int ticketId, int? operatorId, bool isManualOverride)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return CheckInResult.Fail(CheckInFailReason.TicketNotFound);
            }

            await SettleIfPastEndAsync(ticket);

            // 出入狀態判斷
            var lastLog = await GetLastInOutLogAsync(ticketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;

            // 終態判斷(是否已有終態)
            if (ticket.Status == (byte)EntryTicketStatus.Cancelled)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyCancelled);
            }
            if (ticket.Status == (byte)EntryTicketStatus.Completed)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyCompleted);
            }
            // 已失效且人不在場內 → 已失效(已完成)
            if (ticket.Status == (byte)EntryTicketStatus.Expired && !isInside)
            {
                return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.AlreadyExpired);
            }

            // 已失效但人還在場內（超時失效）→ 繳費放行，唯一允許的動作
            /////////////////////
            // TODO：之後接超時費用
            if (ticket.Status == (byte)EntryTicketStatus.Expired && isInside)
            {
                OvertimeFeeResult? fee = await CalculateOvertimeFeeAsync(ticket);
                if (fee == null)
                {
                    return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.InvalidSequence);
                }
                if (!fee.IsFree)
                {
                    bool needPaid = await EnsurePendingChargeAsync(ticket, fee);
                    if (needPaid)
                    {
                        return await LogAndFailAsync(ticketId, CheckInAction.CheckOut, operatorId, isManualOverride, CheckInFailReason.ConfirmOvertimeCharge);
                    }
                }
                ticket.Status = (byte)EntryTicketStatus.Completed;
            }
            /////////////////////

            bool isValid = isInside;

            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.CheckOut,
                ActionTime = _time.Now,
                IsValid = isValid,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId,
                FailReason = isValid ? null : (byte)CheckInFailReason.InvalidSequence
            });
            await _db.SaveChangesAsync();
            if (isValid)
            {
                await _visitReviewTicketFactory.RecordVisitEndTimeAsync(ticketId);
            }
            return isValid ? CheckInResult.Ok() : CheckInResult.Fail(CheckInFailReason.InvalidSequence);
        }

        public async Task<OvertimePaymentResult> OvertimeCheckOutAsync(int ticketId, int operatorId, bool isWaived, PaymentMethodOnSite? method, string? remark)
        {
            var ticket = await _db.EntryTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return OvertimePaymentResult.Fail("找不到這張票券");
            }

            var lastLog = await GetLastInOutLogAsync(ticketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;
            if (ticket.Status != (byte)EntryTicketStatus.Expired || !isInside)
            {
                return OvertimePaymentResult.Fail("這張票券目前不需要結帳（可能已處理過）");
            }

            if (!isWaived && method == null)
            {
                return OvertimePaymentResult.Fail("需選擇付款方式");
            }

            // 檢查是否已有超時紀錄與付款紀錄
            var chargelog = await _db.OvertimeCharges.FirstOrDefaultAsync(o => o.OrderId == ticket.OrderId);
            if (chargelog == null)
            {
                return OvertimePaymentResult.Fail("無超時費用紀錄");
            }

            //檢查待付款
            if (chargelog.Status == (byte)OvertimeChargeStatus.Pending)
            {
                // 員工放行
                if (isWaived)
                {
                    chargelog.Status = (byte)OvertimeChargeStatus.Waived;
                    chargelog.PaymentMethod = null;
                }
                else
                {
                    chargelog.Status = (byte)OvertimeChargeStatus.Paid;
                    chargelog.PaymentMethod = (byte)method!.Value; // 有值
                }
                chargelog.OperatorId = operatorId;
                chargelog.ConfirmedAt = _time.Now;
                chargelog.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
            }

            //(一次儲存同一個交易)放行並更改票券, 記錄出場 => 評論也能使用 => 回傳超時付款金額
            ticket.Status = (byte)EntryTicketStatus.Completed;
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)CheckInAction.CheckOut,
                ActionTime = _time.Now,
                IsValid = true,
                IsManualOverride = true,
                OperatorId = operatorId,
                FailReason = null
            });

            await _db.SaveChangesAsync();

            await _visitReviewTicketFactory.RecordVisitEndTimeAsync(ticketId);

            return OvertimePaymentResult.OK(chargelog.ChargeAmount);
        }


        private async Task<(DateOnly BookingDate, TimeOnly StartTIme, TimeOnly EndTime)?> GetReservationTime(int orderId)
        {
            // 根據票券查詢訂單對應的預約時間
            var data = await (from o in _db.Orders
                              join r in _db.Reservations on o.ReservationId equals r.ReservationId
                              where o.OrderId == orderId
                              select new { r.BookingDate, r.StartTime, r.EndTime })
                             .FirstOrDefaultAsync();

            return data == null ? null : (data.BookingDate, data.StartTime, data.EndTime);
        }


        private async Task<CheckInLog?> GetLastInOutLogAsync(int ticketId)
        {
            // 查詢出入場紀錄 --> 需要isValid判斷(ex: 早到也會有一筆失敗進場紀錄, 再刷一次會出現重複進場)
            return await _db.CheckInLogs
                .Where(l => l.TicketId == ticketId && l.IsValid &&
                    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .OrderByDescending(l => l.ActionTime)
                .FirstOrDefaultAsync();
        }

        // 計算超時抽程共用方法
        private async Task<OvertimeFeeResult?> CalculateOvertimeFeeAsync(EntryTicket ticket)
        {
            var reservationDatas = await GetReservationTime(ticket.OrderId);
            if (reservationDatas == null)
            {
                return null;
            }

            DateTime endAt = reservationDatas.Value.BookingDate.ToDateTime(reservationDatas.Value.EndTime);
            return OvertimeFee.Calculate(endAt, _time.Now);
        }

        private async Task<bool> EnsurePendingChargeAsync(EntryTicket ticket, OvertimeFeeResult fee)
        {
            var overTimeCharge = await _db.OvertimeCharges.FirstOrDefaultAsync(o => o.OrderId == ticket.OrderId);

            if (overTimeCharge?.Status == (byte)OvertimeChargeStatus.Paid || overTimeCharge?.Status == (byte)OvertimeChargeStatus.Waived)
            {
                return false;
            }

            if (overTimeCharge?.Status == (byte)OvertimeChargeStatus.Pending)
            {
                return true;
            }

            _db.OvertimeCharges.Add(new OvertimeCharge
            {
                OrderId = ticket.OrderId,
                TicketId = ticket.TicketId,
                OverdueMinutes = fee.OverdueMinutes,
                UnitPrice = fee.UnitPrice,
                BillingHours = fee.BillingHours,
                ChargeAmount = fee.ChargeAmount,
                Status = (byte)OvertimeChargeStatus.Pending,
                PaymentMethod = null,
                Remark = null,
                OperatorId = null,
                CreatedAt = _time.Now,
                ConfirmedAt = null
            });

            return true;
        }


        // 所有超過預約區間的情況(票券會變失效) -> 終態結算
        // 透過現在時間 > EndTime && 現在的票券狀態 && 出入場狀態 
        private async Task<bool> SettleIfPastEndAsync(EntryTicket ticket)
        {
            // 只有 有效 / 已使用 需要結算，失效、取消、完成都已經是終態
            if (ticket.Status != (byte)EntryTicketStatus.Valid &&
                ticket.Status != (byte)EntryTicketStatus.Used)
                return false;

            var reservation = await GetReservationTime(ticket.OrderId);
            if (reservation == null) return false;

            var endAt = reservation.Value.BookingDate.ToDateTime(reservation.Value.EndTime);
            if (_time.Now <= endAt) return false;

            var lastLog = await GetLastInOutLogAsync(ticket.TicketId);
            bool isInside = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;

            // 判斷是否使用 -> 未使用失效(預約未到)
            if (ticket.Status == (byte)EntryTicketStatus.Valid)
                ticket.Status = (byte)EntryTicketStatus.Expired;
            // 已使用且以人在場內 -> 超時失效
            else if (isInside)
                ticket.Status = (byte)EntryTicketStatus.Expired;
            // 使用完畢
            else
                ticket.Status = (byte)EntryTicketStatus.Completed;

            await _db.SaveChangesAsync();
            return true;
        }


        private async Task<CheckInResult> LogAndFailAsync(int ticketId, CheckInAction action, int? operatorId, bool isManualOverride, CheckInFailReason reason)
        {
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticketId,
                Action = (byte)action,
                ActionTime = _time.Now,
                IsValid = false,
                IsManualOverride = isManualOverride,
                OperatorId = operatorId,
                FailReason = (byte)reason
            });
            await _db.SaveChangesAsync();
            return CheckInResult.Fail(reason);
        }
    }
}