using Microsoft.AspNetCore.Mvc.Rendering;
using VenueGo.Data;
using VenueGo.Dtos.VenueDtos;
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


        //建立依運動類型分組、排序好的完整分組清單 >> QueryGroupedBySportType()、GetPageBySportTypeId() 共用
        //只列出底下有存在場地的運動類型,依 SportTypeId 排序
        private List<VenueGroupViewModel> BuildSportTypeGroups()
        {
            //先撈出所有場地
            List<CVenueWrap> allVenues = QueryAll();

            //撈運動類型清單 >> 對照 SportTypeId 對應的名稱
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

            //依 SportTypeId 排序先後,固定分組的順序
            allGroups.Sort((groupA, groupB) => groupA.SportTypeId.CompareTo(groupB.SportTypeId));

            return allGroups;
        }

        //場地依運動類型分組,再依分組做換頁 >> 專供 VenueIndex 頁面使用
        public VenueIndexViewModel QueryGroupedBySportType(int page, int pageSize)
        {
            //建立依運動類型分組、排序好的完整分組清單
            List<VenueGroupViewModel> allGroups = BuildSportTypeGroups();

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

        //查出運動類型在場地列表中的第幾頁 >> 新增、編輯場地後回到該場地所在的頁面
        //分組清單跟 VenueIndex 使用同一份(BuildSportTypeGroups),算出的頁數才會跟列表一致
        //sportTypeId:要找的運動類型;pageSize:每頁顯示幾組運動類型
        public int GetPageBySportTypeId(int sportTypeId, int pageSize)
        {
            //取得依運動類型分組、排序好的完整分組清單
            List<VenueGroupViewModel> allGroups = BuildSportTypeGroups();

            //從第一組開始找,找到該運動類型時,i 就是它在清單中的位置(從0開始)
            //用 for 而不用 foreach:需要索引 i 才知道是第幾組
            for (int i = 0; i < allGroups.Count; i++)
            {
                if (allGroups[i].SportTypeId == sportTypeId)
                {
                    //位置除以每頁組數(整數除法會捨去小數),再加1換算成頁數
                    //例如位置4、每頁3組 >> 4 / 3 = 1 >> 第2頁
                    return i / pageSize + 1;
                }
            }

            //找不到(運動類型不存在或底下沒有存在的場地) >> 回到第1頁
            return 1;
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



        //撈取SportType提供給VenueCreate送到前端產生下拉選單(VenueIndex 的分組也使用)
        //只列出存在的運動類型(IsActive == true):SportTypes 的 IsActive 是軟刪除用的系統欄位,
        //已刪除的運動類型不能再被選來建立場地,否則會出現「場地還在、運動類型已被刪除」的資料
        public List<SelectListItem> GetSportTypes()
        {
            List<SelectListItem> list = new List<SelectListItem>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from t in db.SportTypes
                            where t.IsActive == true
                            select t;

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

        //查出某個運動類型底下「存在的場地」名稱 >> 刪除運動類型、刪除價格規則前的連動檢查共用
        //Venues 的 IsActive 是軟刪除用的系統欄位,IsActive = false 的場地已經不存在,不算連動
        //回傳名稱清單(依 VenueId 排序),呼叫方可以用 Count 判斷有沒有連動,也可以把名稱放進錯誤訊息
        public List<string> QueryActiveVenueNamesBySportType(int sportTypeId)
        {
            List<string> names = new List<string>();

            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from v in db.Venues
                            where v.SportTypeId == sportTypeId && v.IsActive == true
                            orderby v.VenueId
                            select v.VenueName;

                foreach (string name in datas)
                {
                    names.Add(name);
                }
            }

            return names;
        }


        //場地編輯
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Edit(VenueEditViewModel vm, int userId, DateTime now)
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

                    //稽核欄位:記錄最後修改時間與修改者
                    VenueDb.UpdatedAt = now;
                    VenueDb.UpdatedBy = userId;
                }
                db.SaveChanges();
            }
        }


        //檢查場地名稱是否重複(新增、編輯共用)
        //excludeId:編輯時排除自己,新增時傳入null
        public bool IsVenueNameDuplicate(string venueName, int? excludeId)
        {
            //進資料庫撈出場地
            //未被軟刪除、名稱相同、排除自己(編輯時) >> 只要有一筆符合條件就算重複
            using (dbVenueContext db = new dbVenueContext())
            {
                var data = from v in db.Venues
                           where (v.VenueName == venueName) && (v.IsActive == true) && (excludeId == null || v.VenueId != excludeId)
                           select v;
                return data.Any();
            }
        }



        //場地刪除(軟刪除)
        //userId / now 由 Controller 傳入(登入者 UserId、ITimeService 校時後的時間)
        public void Delete(int id, int userId, DateTime now)
        {
            Venue data = null;

            using (dbVenueContext db = new dbVenueContext())
            {
                //依傳入的id去撈取對應的Venue物件
                data = db.Venues.FirstOrDefault(p => p.VenueId == id);

                if (data != null)
                {
                    data.IsActive = false;

                    //軟刪除也是一次更新 >> 記錄是誰、何時停用
                    data.UpdatedAt = now;
                    data.UpdatedBy = userId;
                }
                db.SaveChanges();
            }
        }


        /*組合DTO*/
        //前台首頁的"場館介紹"資料DTO >> GET /api/venues
        public List<VenueCardDto> QueryVenueCards()
        {
            using (dbVenueContext db = new dbVenueContext())
            {
                var datas = from v in db.Venues
                            //1. 每個場地找出它所屬的運動類型(兩邊的 SportTypeId 相同)
                            join s in db.SportTypes on v.SportTypeId equals s.SportTypeId
                            //2. 場地、運動類型都要是未刪除
                            where v.IsActive == true && s.IsActive == true
                            //3. 依運動類型排序,同一運動再依場地 Id 排序
                            orderby v.SportTypeId, v.VenueId
                            //4. 只取前台需要的欄位,轉成 VenueCardDto
                            select new VenueCardDto
                            {
                                VenueId = v.VenueId,
                                VenueName = v.VenueName,
                                SportTypeId = v.SportTypeId,
                                SportTypeName = s.SportName,
                                PhotoPath = v.PhotoPath
                            };

                //5. 執行查詢,轉成 List 回傳
                return datas.ToList();
            }
        }
    }
}