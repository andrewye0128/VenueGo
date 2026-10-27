using Microsoft.EntityFrameworkCore;
using VenueGo.Data;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CVenueUnavailableSlotFactory
    {
        //查詢某場地某天已經被標記不開放的所有時段 >> 給VenueUnavailableSlotManage頁面用,
        //一次查詢撈出整天的資料,不用每個時段各查一次DB
        public List<CVenueUnavailableSlotWrap> QueryByVenueAndDate(int venueId, DateOnly date)
        {
            List<CVenueUnavailableSlotWrap> list = new List<CVenueUnavailableSlotWrap>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from s in db.VenueUnavailableSlots
                            where s.VenueId == venueId && s.UnavailableDate == date
                            select s;

                //將撈取資料放回 list
                foreach (var data in datas)
                {
                    CVenueUnavailableSlotWrap wrap = new CVenueUnavailableSlotWrap();
                    wrap.venueUnavailableSlot = data;
                    list.Add(wrap);
                }
            }

            return list;
        }


        //查詢某場地某天被預約佔用的所有時段 >> 給VenueUnavailableSlotManage頁面提醒後台人員,只讀不寫
        //1. 用 ReservationSlots 判斷佔用,跟前台選時段(TimeSlotService)一樣:預約取消或逾期時那一列會被刪掉,查得到就是目前佔用中
        //2. 一筆預約跨多個時段時,每個時段各回傳一筆,資料相同
        //3. Orders 用 left join,訂單查不到時 OrderId、OrderNo 為 null,頁面改顯示預約編號
        //4. 場地名稱、整筆預約的起訖時間給關閉時段的確認視窗與通知信使用
        public List<CVenueSlotBooking> QueryBookingsByVenueAndDate(int venueId, DateOnly date)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from s in db.ReservationSlots.AsNoTracking()
                            join r in db.Reservations.AsNoTracking() on s.ReservationId equals r.ReservationId
                            join u in db.Users.AsNoTracking() on r.UserId equals u.UserId
                            join v in db.Venues.AsNoTracking() on s.VenueId equals v.VenueId
                            join o in db.Orders.AsNoTracking() on (int?)r.ReservationId equals o.ReservationId into orderGroup
                            from o in orderGroup.DefaultIfEmpty()
                            where s.VenueId == venueId && s.BookingDate == date
                            select new CVenueSlotBooking
                            {
                                SlotTime = s.SlotTime,
                                ReservationId = r.ReservationId,
                                OrderId = o == null ? null : o.OrderId,
                                OrderNo = o == null ? null : o.OrderNo,
                                MemberName = u.Name,
                                MemberEmail = u.Email,
                                ReservationStatus = r.ReservationStatus,
                                VenueName = v.VenueName,
                                BookingDate = r.BookingDate,
                                StartTime = r.StartTime,
                                EndTime = r.EndTime
                            };

                return datas.ToList();
            }
        }


        //依場地+日期+時間查詢單筆 >> 給Toggle判斷目前這個時段是開放還是不開放用,查不到代表目前是開放狀態
        public CVenueUnavailableSlotWrap FindByKey(int venueId, DateOnly date, TimeOnly time)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var data = db.VenueUnavailableSlots.FirstOrDefault(s =>
                    s.VenueId == venueId && s.UnavailableDate == date && s.UnavailableTime == time);

                if (data == null)
                {
                    return null;
                }

                CVenueUnavailableSlotWrap wrap = new CVenueUnavailableSlotWrap();
                wrap.venueUnavailableSlot = data;
                return wrap;
            }
        }


        //新增不開放時段
        public void Create(CVenueUnavailableSlotWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                db.VenueUnavailableSlots.Add(Wrap.venueUnavailableSlot);
                db.SaveChanges();
            }
        }


        //刪除不開放時段(恢復開放) >> 這張表沒有IsActive,不需要軟刪除,直接硬刪除
        public void Delete(int id)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var data = db.VenueUnavailableSlots.FirstOrDefault(s => s.VenueUnavailableSlotId == id);
                if (data != null)
                {
                    db.VenueUnavailableSlots.Remove(data);
                    db.SaveChanges();
                }
            }
        }
    }
}