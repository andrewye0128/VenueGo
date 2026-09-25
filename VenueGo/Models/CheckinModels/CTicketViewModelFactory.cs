using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.Models.Enums;
using VenueGo.ViewModels.CheckinViewModels;

namespace VenueGo.Models.CheckinModels
{
    public class CTicketViewModelFactory
    {
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

        public List<EntryTicketListViewModel> SearchTickets(DateOnly date, string? keyword, int? venueId, int? status)
        {
            dbVenueContext db = new dbVenueContext();
            var query = from t in db.EntryTickets
                        join o in db.Orders on t.OrderId equals o.OrderId
                        join u in db.Users on o.UserId equals u.UserId
                        join r in db.Reservations on o.ReservationId equals r.ReservationId
                        join v in db.Venues on r.VenueId equals v.VenueId
                        where r.BookingDate == date
                        select new { t, o, u, r, v };

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(x => x.u.Name.Contains(keyword)
                                       || x.u.Phone.Contains(keyword)
                                       || x.t.Qrtoken.Contains(keyword));
            }

            if (venueId.HasValue)
            {
                query = query.Where(x => x.v.VenueId == venueId.Value);
            }

            if (status.HasValue)
            {
                byte statusByte = (byte)status.Value;
                query = query.Where(x => x.t.Status == statusByte);
            }

            var datas = query
                .OrderBy(x => x.r.StartTime)
                .Select(x => new EntryTicketListViewModel
                {
                    TicketId = x.t.TicketId,
                    Qrtoken = x.t.Qrtoken,
                    UserName = x.u.Name,
                    VenueName = x.v.VenueName,
                    BookingDate = x.r.BookingDate,
                    StartTime = x.r.StartTime,
                    EndTime = x.r.EndTime,
                    Status = x.t.Status
                });

            return datas.ToList();
        }

        public List<SelectListItem> GetVenueOptions()
        {
            dbVenueContext db = new dbVenueContext();
            return db.Venues
                .OrderBy(v => v.VenueName)
                .Select(v => new SelectListItem
                {
                    Value = v.VenueId.ToString(),
                    Text = v.VenueName
                })
                .ToList();
        }

        public void UpdateTicketStatus(int ticketId)
        {
            dbVenueContext db = new dbVenueContext();
            if (ticketId <= 0)
            {
                return;
            }
            var ticket = db.EntryTickets.FirstOrDefault(t => t.TicketId == ticketId);
            if (ticket == null)
            {
                return;
            }

            if (ticket.Status == (byte)EntryTicketStatus.Valid)
            {
                ticket.Status = (byte)EntryTicketStatus.Used;

                // 寫入一筆假資料
                db.CheckInLogs.Add(new CheckInLog
                {
                    TicketId = ticket.TicketId,
                    Action = 1, //先假預設進為1
                    ActionTime = DateTime.Now,
                    IsValid = true,
                    IsManualOverride = true,
                    OperatorId = 1,
                });

                db.SaveChanges();
            }
        }



        // Detail頁面
        public EntryTicketDetailViewModel? GetTicketDetail(int ticketId)
        {
            dbVenueContext db = new dbVenueContext();
            var data = (from t in db.EntryTickets
                        join o in db.Orders on t.OrderId equals o.OrderId
                        join u in db.Users on o.UserId equals u.UserId
                        join r in db.Reservations on o.ReservationId equals r.ReservationId
                        join v in db.Venues on r.VenueId equals v.VenueId
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

            data.Logs = db.CheckInLogs
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
