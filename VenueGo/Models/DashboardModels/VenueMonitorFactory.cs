using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Enums;
using VenueGo.Services;
using VenueGo.Services.CheckIn;
using VenueGo.ViewModels.DashboardViewModels;


namespace VenueGo.Models.DashboardModels
{
    public class VenueMonitorFactory
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _time;

        public VenueMonitorFactory(dbVenueContext db, ITimeService time)
        {
            _db = db;
            _time = time;
        }

        public async Task<List<VenueOccupancyViewModel>> GetVenueOccupancyAsync()
        {
            var insideTicketIds = await GetInsideTicketIdsAsync();

            var occupancyByVenue = await _db.EntryTickets
                .Where(t => insideTicketIds.Contains(t.TicketId))
                .Join(_db.Orders, t => t.OrderId, o => o.OrderId, (t, o) => o)
                .Join(_db.Reservations, o => o.ReservationId, r => r.ReservationId, (o, r) => r.VenueId)
                .GroupBy(venueId => venueId)
                .Select(g => new { VenueId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VenueId, x => x.Count);

            var venues = await _db.Venues
                .Where(v => v.IsActive)
                .OrderBy(v => v.VenueName)
                .ToListAsync();

            return venues.Select(v => new VenueOccupancyViewModel
            {
                VenueId = v.VenueId,
                VenueName = v.VenueName,
                Location = v.Location,
                PhotoPath = v.PhotoPath,
                Capacity = v.Capacity,
                CurrentCount = occupancyByVenue.TryGetValue(v.VenueId, out var c) ? c : 0
            }).ToList();
        }

        // 新增:逾期未離場名單
        public async Task<List<OverdueTicketViewModel>> GetOverdueListAsync()
        {
            var insideTicketIds = await GetInsideTicketIdsAsync();

            var insideDetails = await (from t in _db.EntryTickets
                                       join o in _db.Orders on t.OrderId equals o.OrderId
                                       join u in _db.Users on o.UserId equals u.UserId
                                       join r in _db.Reservations on o.ReservationId equals r.ReservationId
                                       join v in _db.Venues on r.VenueId equals v.VenueId
                                       where insideTicketIds.Contains(t.TicketId)
                                       select new OverdueTicketViewModel
                                       {
                                           TicketId = t.TicketId,
                                           OrderId = t.OrderId,
                                           Qrtoken = t.Qrtoken,
                                           VenueName = v.VenueName,
                                           UserName = u.Name,
                                           BookingDate = r.BookingDate,
                                           StartTime = r.StartTime,
                                           EndTime = r.EndTime,
                                       }).ToListAsync();

            // DateOnly+TimeOnly 組合比較放在 C# 端做(EF Core 翻譯不穩定,跟排程那邊踩過的坑一樣)
            var now = _time.Now;
            var overdueList = insideDetails
                .Where(x => x.EndAt < now)
                .OrderBy(x => x.EndAt)
                .ToList();

            var orderIds = overdueList.Select(x => x.OrderId).ToList();
            var charges = await _db.OvertimeCharges
                .AsNoTracking()
                .Where(c => orderIds.Contains(c.OrderId))
                .ToDictionaryAsync(c => c.OrderId);

            foreach (var item in overdueList)
            {
                item.OverdueDuration = now - item.EndAt;

                // 預估:永遠用「現在」算
                var fee = OvertimeFee.Calculate(item.EndAt, now);
                item.IsFree = fee.IsFree;
                item.EstimatedAmount = fee.ChargeAmount;

                // 凍結:只有這個訂單真的有紀錄才填
                if (charges.TryGetValue(item.OrderId, out var charge))
                {
                    item.ChargeStatus = charge.Status;
                    item.ChargeAmount = charge.ChargeAmount;
                }
            }

            return overdueList;
        }

        // 共用:目前在場的票(最後一筆有效紀錄是入場)
        private async Task<List<int>> GetInsideTicketIdsAsync()
        {
            var lastLogTimes = _db.CheckInLogs
                .Where(l => l.IsValid &&
                    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .GroupBy(l => l.TicketId)
                .Select(g => new { TicketId = g.Key, LastTime = g.Max(l => l.ActionTime) });

            return await _db.CheckInLogs
                .Where(l => l.IsValid &&
                    (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .Join(lastLogTimes,
                      l => new { l.TicketId, l.ActionTime },
                      x => new { x.TicketId, ActionTime = x.LastTime },
                      (l, x) => l)
                .Where(l => l.Action == (byte)CheckInAction.CheckIn)
                .Select(l => l.TicketId)
                .ToListAsync();
        }
    }
    
}
