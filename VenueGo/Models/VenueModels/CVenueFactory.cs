using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.ViewModels;

namespace VenueGo.Models.VenueModels
{
    public class CVenueFactory
    {

        //回傳所有場地資料 >> List
        public List<CVenueWrap> QueryAll()
        {
            List<CVenueWrap> list = new List<CVenueWrap>();
            IQueryable<Venue> datas = null;

            using (dbVenueContext db = new dbVenueContext())
            {
                datas = from t in db.Venues
                        where t.IsActive == true
                        select t;


                //將撈取資料放回 list
                foreach (var data in datas)
                {
                    CVenueWrap x = new CVenueWrap();
                    x.venue = data;
                    list.Add(x);
                }
            }
            return list;
        }

        //依 id 搜尋場地
        public CVenueWrap QueryById(int id)
        {
            CVenueWrap VenueWrap = new CVenueWrap();

            dbVenueContext db = new dbVenueContext();
            var VenueDb = db.Venues.FirstOrDefault(p => p.VenueId == id);
            if (VenueDb == null)
                return null;

            VenueWrap.venue = VenueDb;
            return VenueWrap;
        }


        //場地新增
        public void Create(CVenueWrap Wrap)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                db.Venues.Add(Wrap.venue);
                db.SaveChanges();
            }
        }



        //撈取SportType提供給VenueCreate送到前端產生下拉選單
        public List<SelectListItem> GetSportTypes()
        {
            List<SelectListItem> list = new List<SelectListItem>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from t in db.SportTypes select t;
                ;
                foreach (var data in datas)
                {
                    list.Add(new SelectListItem
                    {
                        Text = data.SportName,
                        Value = data.SportTypeId.ToString()
                    });
                }
            }
            return list;
        }

        //場地編輯
        public void Edit(VenueEditViewModel vm)
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                //依照vm傳來的id查找對應Venue物件
                var VenueDb = db.Venues.FirstOrDefault(p => p.VenueId == vm.VenueId);
                    //驗證是否找到資料
                    if (VenueDb != null)
                {
                    //將vm內部資料覆蓋掉VenueDb原有資料
                    VenueDb.VenueName = vm.VenueName;
                    VenueDb.Location = vm.Location;
                    VenueDb.Capacity = vm.Capacity;
                    VenueDb.SportTypeId = vm.SportTypeId;

                    if (vm.PhotoFile != null)
                        VenueDb.PhotoPath = vm.PhotoPath;
                }
                db.SaveChanges();
            }
        }


        //場地刪除
        public void Delete(int id)
        {
            Venue data = null;

            using (dbVenueContext db = new dbVenueContext())
            {
                //依傳入的id去撈取對應的Venue物件
                data = db.Venues.FirstOrDefault(p => p.VenueId == id);

                if (data != null)
                    data.IsActive = false;
                db.SaveChanges();
            }
        }
    }
}