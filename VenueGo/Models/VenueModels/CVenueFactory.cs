using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Models.Entities;
using VenueGo.ViewModels.VenueViewModels;

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


        //場地依運動類型分組,再依分組做換頁 >> 專供 VenueIndex 頁面使用
        //分頁的單位是「運動類型分組」,不是「場地」
        //例如 pageSize = 3,代表一頁顯示 3 種運動類型,每種底下的場地都會整組顯示,不會被拆到下一頁
        public VenueIndexViewModel QueryGroupedBySportType(int page, int pageSize)
        {
            //先撈出所有場地,沿用既有方法,不重複寫一次查詢邏輯
            List<CVenueWrap> allVenues = QueryAll();

            //撈運動類型清單,用來對照 SportTypeId 對應的名稱
            List<SelectListItem> sportTypes = GetSportTypes();

            //把場地依 SportTypeId 分組,一種運動類型一組
            List<VenueGroupViewModel> allGroups = new List<VenueGroupViewModel>();

            foreach (SelectListItem sportType in sportTypes)
            {
                int sportTypeId = int.Parse(sportType.Value);

                //把屬於這個運動類型的場地一筆一筆挑出來
                List<CVenueWrap> venuesInThisGroup = new List<CVenueWrap>();
                foreach (CVenueWrap venue in allVenues)
                {
                    if (venue.SportTypeId == sportTypeId)
                    {
                        venuesInThisGroup.Add(venue);
                    }
                }

                //這個運動類型底下如果沒有任何場地,就不用顯示這一組,略過
                if (venuesInThisGroup.Count == 0)
                {
                    continue;
                }

                VenueGroupViewModel group = new VenueGroupViewModel();
                group.SportTypeId = sportTypeId;
                group.SportTypeName = sportType.Text;
                group.Venues = venuesInThisGroup;

                allGroups.Add(group);
            }

            //依 SportTypeId 排序,固定分組的順序
            //如果沒有排序,換頁時分組的先後順序可能會不穩定,同一組今天在第1頁、明天卻跑到第2頁
            allGroups.Sort((groupA, groupB) => groupA.SportTypeId.CompareTo(groupB.SportTypeId));

            //頁碼防呆:小於1就修正回第1頁
            if (page < 1)
            {
                page = 1;
            }

            VenueIndexViewModel vm = new VenueIndexViewModel();
            vm.PageSize = pageSize;
            vm.TotalGroupCount = allGroups.Count;

            //頁碼超過總頁數(例如原本第3頁,結果分組被刪到只剩1頁),修正回最後一頁
            //避免畫面直接空白,讓使用者以為資料不見了
            if (page > vm.TotalPages)
            {
                page = vm.TotalPages;
            }

            vm.CurrentPage = page;

            //從分組清單裡,把目前這一頁該顯示的分組挑出來
            //例如page=2、pageSize=3 >> 從第4組開始挑,挑到第6組為止
            int startIndex = (page - 1) * pageSize;

            List<VenueGroupViewModel> groupsForThisPage = new List<VenueGroupViewModel>();
            for (int i = startIndex; i < allGroups.Count && i < startIndex + pageSize; i++)
            {
                groupsForThisPage.Add(allGroups[i]);
            }

            vm.Groups = groupsForThisPage;

            return vm;
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