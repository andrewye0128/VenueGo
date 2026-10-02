using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.CheckinViewModels;

namespace VenueGo.Models.CheckinModels
{
    public class CTicketViewModelFactory
    {

        private readonly dbVenueContext _db;

        public CTicketViewModelFactory(dbVenueContext db)
        {
            _db = db;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        //public List<EntryTicketListViewModel> AllTicketListByDay(DateOnly date)
        //{
        //    //List<EntryTicketListViewModel> list = new List<EntryTicketListViewModel>();
        //    dbVenueContext db = new dbVenueContext();
        //    var datas = from t in db.EntryTickets
        //                join o in db.Orders on t.OrderId equals o.OrderId
        //                join u in db.Users on o.UserId equals u.UserId
        //                join r in db.Reservations on o.ReservationId equals r.ReservationId
        //                join v in db.Venues on r.VenueId equals v.VenueId
        //                where r.BookingDate == date
        //                orderby r.StartTime
        //                select new EntryTicketListViewModel
        //                {
        //                    TicketId = t.TicketId,
        //                    Qrtoken = t.Qrtoken,
        //                    UserName = u.Name,
        //                    VenueName = v.VenueName,
        //                    BookingDate = r.BookingDate,
        //                    StartTime = r.StartTime,
        //                    EndTime = r.EndTime,
        //                    Status = t.Status
        //                };

        //    return datas.ToList();
        //}

        //public List<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status)
        //{
        //    dbVenueContext db = new dbVenueContext();
        //    var query = from t in db.EntryTickets
        //                join o in db.Orders on t.OrderId equals o.OrderId
        //                join u in db.Users on o.UserId equals u.UserId
        //                join r in db.Reservations on o.ReservationId equals r.ReservationId
        //                join v in db.Venues on r.VenueId equals v.VenueId
        //                where r.BookingDate == date
        //                select new { t, o, u, r, v };

        //    if (!string.IsNullOrWhiteSpace(keyword))
        //    {
        //        query = query.Where(x => x.u.Name.Contains(keyword)
        //                               || x.u.Phone.Contains(keyword)
        //                               || x.t.Qrtoken.Contains(keyword));
        //    }

        //    if (venueId.HasValue)
        //    {
        //        query = query.Where(x => x.v.VenueId == venueId.Value);
        //    }

        //    if (status.HasValue)
        //    {
        //        byte statusByte = (byte)status.Value;
        //        query = query.Where(x => x.t.Status == statusByte);
        //    }

        //    var datas = query
        //        .OrderBy(x => x.r.StartTime)
        //        .Select(x => new EntryTicketListViewModel
        //        {
        //            TicketId = x.t.TicketId,
        //            Qrtoken = x.t.Qrtoken,
        //            UserName = x.u.Name,
        //            VenueName = x.v.VenueName,
        //            BookingDate = x.r.BookingDate,
        //            StartTime = x.r.StartTime,
        //            EndTime = x.r.EndTime,
        //            Status = x.t.Status
        //        });

        //    return datas.ToList();
        //}

        public List<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status)
        {
            return _db.Database.SqlQuery<EntryTicketListViewModel>($@"
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
            if (isVaild && ticket.Status == (byte)EntryTicketStatus.Valid)
            {
                ticket.Status = (byte)EntryTicketStatus.Used;
            }
            // 寫入一筆假資料
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticket.TicketId,
                Action = 1, //先假預設進為1
                ActionTime = DateTime.Now,
                IsValid = isVaild,
                IsManualOverride = true,
                OperatorId = 1,
            });

            _db.SaveChanges();
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
            bool isVaild = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;
            if (!isVaild)
            {
                return false;
            }
            // 寫入一筆假資料
            _db.CheckInLogs.Add(new CheckInLog
            {
                TicketId = ticket.TicketId,
                Action = 2, //先假預設出場
                ActionTime = DateTime.Now,
                IsValid = isVaild,
                IsManualOverride = true,
                OperatorId = 1,
            });
            _db.SaveChanges();
            return isVaild;
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


        // Detail頁面
        public EntryTicketDetailViewModel? GetTicketDetail(int ticketId)
        {
            var data = (from t in _db.EntryTickets
                        join o in _db.Orders on t.OrderId equals o.OrderId
                        join u in _db.Users on o.UserId equals u.UserId
                        join r in _db.Reservations on o.ReservationId equals r.ReservationId
                        join v in _db.Venues on r.VenueId equals v.VenueId
                        where t.TicketId == ticketId
                        select new EntryTicketDetailViewModel
                        {
                            TicketId = t.TicketId,
                            Qrtoken = t.Qrtoken,
                            UserName = u.Name,
                            Status = t.Status,
                            VenueName = v.VenueName,
                            Location = v.Location,
                            BookingDate = r.BookingDate,
                            StartTime = r.StartTime,
                            EndTime = r.EndTime
                        }).FirstOrDefault();

            if (data == null) return null;

            data.Logs = _db.CheckInLogs
                .Where(l => l.TicketId == ticketId)
                .OrderByDescending(l => l.ActionTime)
                .Select(l => new CheckInLogViewModel
                {
                    Action = l.Action,
                    ActionTime = l.ActionTime,
                    IsManualOverride = l.IsManualOverride,
                    IsValid = l.IsValid,
                    OperatorId = l.OperatorId
                }).ToList();

            return data;
        }


        public async Task<string?> GetQrtokenAsync(int ticketId)
        {
            return await _db.EntryTickets
                .Where(t => t.TicketId == ticketId)
                .Select(t => t.Qrtoken)
                .FirstOrDefaultAsync();
        }

        // 使用bool方法表示[這次操作在流程上合不合理], 只有符合異常流程出現時會需要使用的方法
        //public bool ManualCheckIn(int ticketId, int operatorId)
        //{
        //    dbVenueContext db = new dbVenueContext();
        //    var ticket = db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
        //    if (ticket == null) return false;

        //    var lastLog = db.CheckInLogs
        //    .Where(l => l.TicketId == ticketId &&
        //    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
        //    .OrderByDescending(l => l.ActionTime)
        //    .FirstOrDefault();

        //    // 沒有紀錄或上一筆是離場
        //    bool isValid = lastLog == null || lastLog.Action == (byte)CheckInAction.CheckOut;

        //    if (isValid && ticket.Status == (byte)EntryTicketStatus.Valid)
        //        ticket.Status = (byte)EntryTicketStatus.Used;

        //    db.CheckInLogs.Add(new CheckInLog
        //    {
        //        TicketId = ticketId,
        //        Action = (byte)CheckInAction.CheckIn,
        //        ActionTime = DateTime.Now,
        //        IsValid = isValid,
        //        IsManualOverride = true,
        //        OperatorId = operatorId
        //    });
        //    db.SaveChanges();
        //    return isValid;
        //}

        //public bool ManualCheckOut(int ticketId, int operatorId)
        //{
        //    dbVenueContext db = new dbVenueContext();
        //    var ticket = db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
        //    if (ticket == null) return false;

        //    var lastLog = db.CheckInLogs
        //    .Where(l => l.TicketId == ticketId &&
        //    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
        //    .OrderByDescending(l => l.ActionTime)
        //    .FirstOrDefault();

        //    // 上一筆是入場
        //    bool isValid = lastLog != null && lastLog.Action == (byte)CheckInAction.CheckIn;

        //    db.CheckInLogs.Add(new CheckInLog
        //    {
        //        TicketId = ticketId,
        //        Action = (byte)CheckInAction.CheckOut,
        //        ActionTime = DateTime.Now,
        //        IsValid = isValid,
        //        IsManualOverride = true,
        //        OperatorId = operatorId
        //    });
        //    db.SaveChanges();
        //    return isValid;
        //}

        //public bool ManualCancel(int ticketId, int operatorId)
        //{
        //    dbVenueContext db = new dbVenueContext();
        //    var ticket = db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
        //    if (ticket == null || ticket.Status == (byte)EntryTicketStatus.Cancelled) return false;

        //    ticket.Status = (byte)EntryTicketStatus.Cancelled;
        //    db.CheckInLogs.Add(new CheckInLog
        //    {
        //        TicketId = ticketId,
        //        Action = (byte)CheckInAction.ManualCancel,
        //        ActionTime = DateTime.Now,
        //        IsValid = true,
        //        IsManualOverride = true,
        //        OperatorId = operatorId
        //    });
        //    db.SaveChanges();
        //    return true;
        //}

        //public bool ManualExpire(int ticketId, int operatorId)
        //{
        //    dbVenueContext db = new dbVenueContext();
        //    var ticket = db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
        //    if (ticket == null || ticket.Status == (byte)EntryTicketStatus.Expired) return false;

        //    ticket.Status = (byte)EntryTicketStatus.Expired;
        //    db.CheckInLogs.Add(new CheckInLog
        //    {
        //        TicketId = ticketId,
        //        Action = (byte)CheckInAction.ManualExpire,
        //        ActionTime = DateTime.Now,
        //        IsValid = true,
        //        IsManualOverride = true,
        //        OperatorId = operatorId
        //    });
        //    db.SaveChanges();
        //    return true;
        //}
    }
}
