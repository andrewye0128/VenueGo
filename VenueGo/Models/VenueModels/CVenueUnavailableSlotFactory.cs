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