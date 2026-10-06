using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Enums;

namespace VenueGo.Models.CheckinModels
{
    public class CCheckInLogFactory
    {
        private readonly dbVenueContext _db;

        public CCheckInLogFactory(dbVenueContext db)
        {
            _db = db;
        }

        // 掃描紀錄查詢 -> 給報到管理顯示, 只有進出場的紀錄
        public List<CCheckInLogWrap> QueryByTicket(int ticketId)
        {
            List<CCheckInLogWrap> list = new List<CCheckInLogWrap>();

            var datas = _db.CheckInLogs.AsNoTracking()
                .Where(l => l.TicketId == ticketId &&
                            (l.Action == (byte)CheckInAction.CheckIn || l.Action == (byte)CheckInAction.CheckOut))
                .OrderByDescending(l => l.ActionTime)
                .ThenByDescending(l => l.LogId)
                .ToList();

            foreach (var data in datas)
            {
                CCheckInLogWrap wrap = new CCheckInLogWrap();
                wrap.checkInLog = data;
                list.Add(wrap);
            }

            return list;
        }
    }
}
