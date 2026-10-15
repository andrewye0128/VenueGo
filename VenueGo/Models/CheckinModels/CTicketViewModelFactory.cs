using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Models.ReviewModels;
using VenueGo.Services.Time;
using VenueGo.Services.ReviewTickets;
using VenueGo.ViewModels.CheckinViewModels;

namespace VenueGo.Models.CheckinModels
{
    public class CTicketViewModelFactory
    {

        private readonly dbVenueContext _db;
        private CCheckInLogFactory _checkinLogFactory;
        private CTicketStatusLogFactory _StatusLogFactory;
        private IVisitReviewTicketFactory _visitReviewTicketFactory;
        private COvertimeChargeFactory _overtimeChargeFactory;
        private readonly ITimeService _time;

        public CTicketViewModelFactory(dbVenueContext db, CCheckInLogFactory checkInLogFactory, CTicketStatusLogFactory statusLogFactory, 
            IVisitReviewTicketFactory visitReviewTicketFactory, COvertimeChargeFactory overtimeChargeFactory, ITimeService time)
        {
            _db = db;
            _checkinLogFactory = checkInLogFactory;
            _StatusLogFactory = statusLogFactory;
            _visitReviewTicketFactory = visitReviewTicketFactory;
            _overtimeChargeFactory = overtimeChargeFactory;
            _time = time;
        }

        public DateOnly GetToday() => DateOnly.FromDateTime(_time.Now);

        //DateOnly today = DateOnly.FromDateTime(DateTime.Now);

        public List<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status)
        {
            List<EntryTicketListViewModel> list =  _db.Database.SqlQuery<EntryTicketListViewModel>($@"
                SELECT TicketId,
                    TicketQRToken     AS Qrtoken,
                    UserName,
                    UserPhone         AS Phone,
                    VenueName,
                    BookingDate,
                    StartTime,
                    EndTime,
                    TicketStatusValue AS Status
                FROM   dbo.v_BookingTicketInfo
                WHERE  TicketId IS NOT NULL
                    AND  BookingDate = {date}
                    AND  ({keyword} IS NULL OR UserName      LIKE '%' + {keyword} + '%'
                                  OR UserPhone     LIKE '%' + {keyword} + '%'
                                  OR TicketQRToken LIKE '%' + {keyword} + '%')
                    AND  ({venueId} IS NULL OR VenueId = {venueId})
                    AND  ({status}  IS NULL OR TicketStatusValue = {status})")
                .OrderBy(x => x.StartTime)
                .ToList();

            // 臨時測試用之後改檢視表
            List<int> test = list.Select(t => t.TicketId).ToList();

            var holders = (from t in _db.EntryTickets.AsNoTracking()
                          join u in _db.Users on t.UserId equals u.UserId
                          where test.Contains(t.TicketId)
                          select new { t.TicketId, u.Name })
                         .ToDictionary(x => x.TicketId, x => x.Name);

            foreach( var item in list )
            {
                item.Holder = holders.ContainsKey(item.TicketId) ? holders[item.TicketId] : item.UserName;
            }

            return list;
        }

        public List<SelectListItem> GetVenueOptions()
        {
            return _db.Venues
                .OrderBy(v => v.VenueName)
                .Select(v => new SelectListItem
                {
                    Value = v.VenueId.ToString(),
                    Text = v.VenueName
                })
                .ToList();
        }


        // -------------------- 測試用待撤離到 ICheckInService.CheckInAsync/CheckOutAsync --------------------
        public async Task<bool> UpdateTicketInStatus(int ticketId)
        {
            if (ticketId <= 0)
            {
                return false;
            }
            var ticket = _db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return false;
            }

            var lastlog = await GetLastInOutLogAsync(ticketId);
            //無紀錄或以出場達成入場條件
            bool isVaild = lastlog == null || lastlog.Action == (byte)CheckInAction.CheckOut;
            bool isFirstEntry = isVaild && ticket.Status == (byte)EntryTicketStatus.Valid;
            if (isFirstEntry)
            {
                ticket.Status = (byte)EntryTicketStatus.Used;
            }
            // 寫入一筆假資料
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticket.TicketId,
                Action = 1, //先假預設進為1
                ActionTime = _time.Now,
                IsValid = isVaild,
                IsManualOverride = true,
                OperatorId = 1,
            });

            _db.SaveChanges();
            if(isFirstEntry)
            {
                await _visitReviewTicketFactory.CreateReviewPerVisitAsync(ticket.Qrtoken);
            }
            return isVaild;
        }

        public async Task<bool> UpdateTicketOutStatus(int ticketId)
        {
            if (ticketId <= 0)
            {
                return false;
            }
            var ticket = _db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return false;
            }

            if (ticket.Status != (byte)EntryTicketStatus.Used)
            {
                return false;
            }

            var lastLog = await GetLastInOutLogAsync(ticketId);
            bool isValid = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;
            if (!isValid)
            {
                return false;
            }
            // 寫入一筆假資料
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticket.TicketId,
                Action = 2, //先假預設出場
                ActionTime = _time.Now,
                IsValid = isValid,
                IsManualOverride = true,
                OperatorId = 1,
            });
            _db.SaveChanges();
            if (isValid)
            {
                await _visitReviewTicketFactory.RecordVisitEndTimeAsync(ticketId);
            }
            return isValid;
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

        // ----------------------------------- end -----------------------------------

        public EntryTicketDetailViewModel? GetDetailTab(int ticketId)
        {
            EntryTicketDetailViewModel vm = new EntryTicketDetailViewModel();
            if (!FillTicketBase(vm, ticketId)) return null;
            return vm;
        }

        //「報到管理」掃描分頁
        public TicketScanTabViewModel? GetScanTab(int ticketId)
        {
            TicketScanTabViewModel vm = new TicketScanTabViewModel();
            if (!FillTicketBase(vm, ticketId)) return null;

            vm.Logs = _checkinLogFactory.QueryByTicket(ticketId);
            return vm;
        }

        //「異動紀錄」包含 log 和 每個異動button(取消, 失效, 轉發) => 到異動工廠篩選選擇的狀態可帶入那些原因並加入到SelectListItem
        public TicketManualTabViewModel? GetManualTab(int ticketId)
        {
            TicketManualTabViewModel vm = new TicketManualTabViewModel();
            if (!FillTicketBase(vm, ticketId)) return null;

            vm.Logs = _StatusLogFactory.QueryByTicket(ticketId);
            vm.CancelReasonOptions = _StatusLogFactory.GetReasonOptions(TicketManualLogAction.Cancel);
            vm.ExpireReasonOptions = _StatusLogFactory.GetReasonOptions(TicketManualLogAction.Expire);
            vm.ReissueReasonOptions = _StatusLogFactory.GetReasonOptions(TicketManualLogAction.Reissue);
            vm.TransferReasonOptions = _StatusLogFactory.GetReasonOptions(TicketManualLogAction.Transfer);
            return vm;
        }

        public TicketOvertimeTabViewModel? GetOvertimeTab(int ticketId)
        {
            TicketOvertimeTabViewModel vm = new TicketOvertimeTabViewModel();
            if (!FillTicketBase(vm, ticketId)) return null;

            vm.Charge = _overtimeChargeFactory.GetByTicket(ticketId);
            vm.PaymentOptions = _overtimeChargeFactory.GetPayOption();
            return vm;
        }



        private bool FillTicketBase(TicketTabViewModelBase vm, int ticketId)
        {
            var data = (from t in _db.EntryTickets.AsNoTracking()
                        join o in _db.Orders on t.OrderId equals o.OrderId
                        join u in _db.Users on t.UserId equals u.UserId
                        join r in _db.Reservations on o.ReservationId equals r.ReservationId
                        join v in _db.Venues on r.VenueId equals v.VenueId
                        where t.TicketId == ticketId
                        select new
                        {
                            t.TicketId,
                            t.Qrtoken,
                            UserName = u.Name,
                            t.Status,
                            v.VenueName,
                            v.Location,
                            r.BookingDate,
                            r.StartTime,
                            r.EndTime
                        }).FirstOrDefault();

            if (data == null) return false;

            vm.TicketId = data.TicketId;
            vm.Qrtoken = data.Qrtoken;
            vm.UserName = data.UserName;
            vm.Status = data.Status;
            vm.VenueName = data.VenueName;
            vm.Location = data.Location;
            vm.BookingDate = data.BookingDate;
            vm.StartTime = data.StartTime;
            vm.EndTime = data.EndTime;
            vm.IsPastEndTime = _time.Now > vm.BookingDate.ToDateTime(vm.EndTime);

            //有效的進出紀錄(新到舊),只看進場、離場
            List<byte> validActions = _db.CheckInLogs.AsNoTracking()
                .Where(l => l.TicketId == ticketId && l.IsValid &&
                            (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .OrderByDescending(l => l.ActionTime)
                .ThenByDescending(l => l.LogId)
                .Select(l => l.Action)
                .ToList();

            //最新一筆是入場 → 人在場內
            vm.IsInside = validActions.Count > 0 && validActions[0] == (byte)CheckInAction.CheckIn;
            vm.HasValidCheckIn = validActions.Contains((byte)CheckInAction.CheckIn);

            return true;
        }

        public async Task<string?> GetQrtokenAsync(int ticketId)
        {
            return await _db.EntryTickets
                .Where(t => t.TicketId == ticketId)
                .Select(t => t.Qrtoken)
                .FirstOrDefaultAsync();
        }
    }
}
