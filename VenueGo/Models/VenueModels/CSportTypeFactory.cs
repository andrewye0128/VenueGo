using System.Reflection.Metadata.Ecma335;
using VenueGo.Data;
using VenueGo.Models.Entities;

namespace VenueGo.Models.VenueModels
{
    public class CSportTypeFactory
    {
        /*對SportType表做CRUD方法集中處*/

        //撈取所有資料 >> QueryAll
        public List<CSportTypeWrap> QueryAll()
        {
            //準備要回傳的變數 >> list
            List<CSportTypeWrap> list = new List<CSportTypeWrap>();
            //撈取資料 >> 撈出集合形式
            dbVenueContext db = new dbVenueContext();
            var datas = from t in db.SportTypes
                        where t.IsActive == true
                        select t;

            //對IQueryable集合元素逐項放入list
            foreach (var item in datas)
            {
                CSportTypeWrap x = new CSportTypeWrap();
                x.sportType = item;
                list.Add(x);
            }

            return list;
        }


        //依照id撈取對應資料 >> QueryById
        public CSportTypeWrap QueryById(int? id)
        {
            CSportTypeWrap SportTypeWrap = new CSportTypeWrap();
            SportType? SportTypeDb = null;
            dbVenueContext db = new dbVenueContext();
            if (id != null)
            {
                SportTypeDb = db.SportTypes.FirstOrDefault(t => t.SportTypeId == (int)id);
            }

            if (SportTypeDb == null)
            {
                return new CSportTypeWrap();
            }

            SportTypeWrap.sportType = SportTypeDb;
            return SportTypeWrap;
        }


        //Create
        public void Create(CSportTypeWrap Wrap)
        {
            dbVenueContext db = new dbVenueContext();
            db.SportTypes.Add(Wrap.sportType);
            db.SaveChanges();
        }

        //Delete(軟刪除)
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Delete(int id, int userId, DateTime now)
        {
            dbVenueContext db = new dbVenueContext();
            //依照取得的id去尋找對應的SportType
            var data = db.SportTypes.FirstOrDefault(item => item.SportTypeId == id);
            if (data != null)
            {
                data.IsActive = false;

                //軟刪除也是一次更新 >> 記錄是誰、何時停用
                data.UpdatedAt = now;
                data.UpdatedBy = userId;
            }
            db.SaveChanges();
        }

        //Edit
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Edit(CSportTypeWrap Wrap, string? newPhotoPath, int userId, DateTime now)
        {
            dbVenueContext db = new dbVenueContext();
            //驗證傳入Wrap非null
            if (Wrap == null)
                return;

            //用傳入Wrap內部的id值查找對應資料
            var SportTypeDb = db.SportTypes.FirstOrDefault(t => t.SportTypeId == Wrap.SportTypeId);
            if (SportTypeDb != null)
            {
                SportTypeDb.SportName = Wrap.SportName;

                //注意事項:後台清空就存 null,保留使用者輸入的換行
                SportTypeDb.Notice = Wrap.Notice;

                //代表照片:有上傳新照片才換,沒上傳就保留資料庫原本的路徑
                //不用 Wrap.PhotoPath >> Wrap 直接接表單,這個值可能被竄改
                if (newPhotoPath != null)
                {
                    SportTypeDb.PhotoPath = newPhotoPath;
                }

                //稽核欄位:記錄最後修改時間與修改者
                SportTypeDb.UpdatedAt = now;
                SportTypeDb.UpdatedBy = userId;
            }

            db.SaveChanges();
        }
    }
}